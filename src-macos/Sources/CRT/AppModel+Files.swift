import AppKit
import Foundation
import CRTCore

// File operations, launch sequence, appearance, tools and window handling.
extension AppModel {

    // MARK: - Launch (called once from ContentView.task)

    @MainActor func onLaunch() async {
        guard !launched else { return }
        launched = true

        applyAppearance()
        libraryEntries = libraryStore.load()
        recentPaths = recents.load()
        foldRecentsIntoLibrary()

        if settings.defaultMode == TimingMode.segments.rawValue {
            files.session.mode = .segments
        }
        syncInputs()

        // Crash restore (spec §14): the autosave file survives only when the
        // previous run did not exit cleanly.
        if autosave.exists, let restored = autosave.restore() {
            let restore = Alerts.confirmYesNo(
                title: loc["Restore Session"],
                message: loc.text(
                    "Restore Session Message",
                    "CRT closed unexpectedly with unsaved changes.\nRestore the last autosaved session?"
                )
            )
            if restore {
                files.session = restored.session
                files.filePath = restored.filePath
                files.dirty = true
                syncInputs()
                page = .frameRetimer
            }
            autosave.clear()
        }

        startAutosaveLoop()

        if settings.enableUpdates {
            updateVersion = await UpdateChecker().latestVersionIfNewer()
        }

        await src.restoreSignIn()
    }

    /// Spec §11.1: the library lists everything in `recent.json` plus every
    /// file the app saved or opened, so recents without a library row (a lost
    /// or older index) are folded in on launch.
    @MainActor private func foldRecentsIntoLibrary() {
        var known = Set(libraryEntries.map { $0.path })
        var entries = libraryEntries
        var added = false
        for path in recentPaths where !known.contains(path) {
            guard FileManager.default.fileExists(atPath: path),
                  let session = try? RunFileStore.load(from: URL(fileURLWithPath: path)) else {
                continue
            }
            var entry = RunLibraryEntry.from(session: session, path: path)
            if let attributes = try? FileManager.default.attributesOfItem(atPath: path),
               let modified = attributes[.modificationDate] as? Date {
                entry.modified = ISO8601DateFormatter().string(from: modified)
            }
            entries.append(entry)
            known.insert(path)
            added = true
        }
        guard added else { return }
        libraryStore.save(entries)
        libraryEntries = entries
    }

    /// Autosave loop (spec §14). The snapshot is taken on the main actor —
    /// the session is main-actor state — and only the disk write is handed to
    /// a detached task.
    @MainActor func startAutosaveLoop() {
        autosaveTask?.cancel()
        let service = autosave
        autosaveTask = Task { @MainActor [weak self] in
            while !Task.isCancelled {
                try? await Task.sleep(nanoseconds: 30_000_000_000)
                guard let self else { return }
                guard self.files.dirty else { continue }
                let snapshot = self.files.session
                let path = self.files.filePath
                await Task.detached {
                    service.save(session: snapshot, filePath: path)
                }.value
            }
        }
    }

    // MARK: - Appearance (spec §12)

    @MainActor func applyAppearance() {
        accentColorHex = settings.accentColorHex
        switch settings.theme {
        case "Dark":
            NSApplication.shared.appearance = NSAppearance(named: .darkAqua)
        case "Light":
            NSApplication.shared.appearance = NSAppearance(named: .aqua)
        default:
            NSApplication.shared.appearance = nil
        }
    }

    // MARK: - Window (always-on-top, spec §6 — ON by default)

    @MainActor func attachWindow(_ window: NSWindow) {
        mainWindow = window
        window.level = alwaysOnTop ? .floating : .normal
    }

    @MainActor func setAlwaysOnTop(_ enabled: Bool) {
        alwaysOnTop = enabled
        mainWindow?.level = enabled ? .floating : .normal
    }

    // MARK: - Dirty prompt (port of `_prompt_save_if_dirty`)

    @MainActor func promptSaveIfDirty(title: String) -> Bool {
        guard files.dirty else { return true }
        switch Alerts.yesNoCancel(
            title: title,
            message: loc["Would you like to save the current time first?"],
            saveTitle: loc["Save"],
            dontSaveTitle: loc["Discard Changes"],
            cancelTitle: loc["Cancel"]
        ) {
        case .cancel:
            return false
        case .no:
            return true
        case .yes:
            saveTime()
            return !files.dirty
        }
    }

    // MARK: - New / Open / Save (spec §6)

    @MainActor func newTime() {
        guard promptSaveIfDirty(title: loc["New Time"]) else { return }
        let mode: TimingMode = settings.defaultMode == TimingMode.segments.rawValue ? .segments : .loads
        files.newSession(mode: mode)
        clearUndo()
        syncInputs()
        video.resetMarks()
        if page == .dashboard {
            page = .frameRetimer
        }
    }

    @MainActor func openTime() {
        guard promptSaveIfDirty(title: loc["Open Time"]) else { return }
        guard let url = Panels.openJSON(title: loc["Open Time"]) else { return }
        if url.path == files.filePath { return }
        openPath(url.path)
    }

    /// Opens a specific path without prompting (callers prompt first).
    @MainActor func openPath(_ path: String) {
        do {
            try files.load(from: path)
            clearUndo()
            syncInputs()
            video.resetMarks()
            recentPaths = recents.add(path)
            libraryEntries = libraryStore.upsert(RunLibraryEntry.from(session: files.session, path: path))
            page = .frameRetimer
        } catch {
            showError(error)
        }
    }

    /// Open action used by the dashboard/library (prompts for dirty state).
    @MainActor func openPathPrompting(_ path: String) {
        guard path != files.filePath else {
            page = .frameRetimer
            return
        }
        guard promptSaveIfDirty(title: loc["Open Time"]) else { return }
        openPath(path)
    }

    @MainActor func saveTime() {
        if files.filePath == nil {
            saveAsTime()
            return
        }
        do {
            try files.save()
            afterSave()
        } catch {
            showError(error)
        }
    }

    @MainActor func saveAsTime() {
        let defaultName: String
        if let path = files.filePath {
            defaultName = URL(fileURLWithPath: path).lastPathComponent
        } else {
            defaultName = "run.json"
        }
        guard var url = Panels.saveJSON(title: loc["Save As"], defaultName: defaultName) else { return }
        if url.pathExtension.lowercased() != "json" {
            url = url.appendingPathExtension("json")
        }
        do {
            try files.saveAs(path: url.path)
            afterSave()
        } catch {
            showError(error)
        }
    }

    @MainActor private func afterSave() {
        guard let path = files.filePath else { return }
        toast(loc["Saved to {path}"].replacingOccurrences(of: "{path}", with: path))
        recentPaths = recents.add(path)
        libraryEntries = libraryStore.upsert(RunLibraryEntry.from(session: files.session, path: path))
        autosave.clear()
    }

    // MARK: - Session history (spec §6)

    @MainActor func openFromHistory(_ path: String) {
        showHistorySheet = false
        guard path != files.filePath else { return }
        guard promptSaveIfDirty(title: loc["Save"]) else { return }
        openPath(path)
    }

    // MARK: - Library actions (spec §11.1)

    @MainActor func revealInFinder(_ path: String) {
        NSWorkspace.shared.activateFileViewerSelecting([URL(fileURLWithPath: path)])
    }

    @MainActor func removeFromLibrary(_ path: String) {
        libraryEntries = libraryStore.remove(path: path)
        recents.remove(path)
        recentPaths = recents.load()
    }

    /// Copies the mod note for a library run without opening it.
    @MainActor func copyModNote(forPath path: String) {
        do {
            let loaded = try RunFileStore.load(from: URL(fileURLWithPath: path))
            let note = ModNoteBuilder.build(template: settings.modNoteFormat, session: loaded)
            Clipboard.set(note)
            toast(loc["Mod note copied"])
        } catch {
            showError(error)
        }
    }

    // MARK: - External tools (spec §8)

    /// Locates a tool, offering to download it when missing.
    @MainActor func ensureTool(_ tool: ExternalTool) async -> URL? {
        if let url = toolLocator().locate(tool) { return url }

        let message = loc.text("Tool Needed", "CRT needs {tool} for this feature. Download it now? ({size})")
            .replacingOccurrences(of: "{tool}", with: tool.displayName)
            .replacingOccurrences(of: "{size}", with: tool.approximateSize)
        guard Alerts.confirmYesNo(title: tool.displayName, message: message) else { return nil }

        toolDownloadName = tool.displayName
        toolDownloadProgress = 0
        defer {
            toolDownloadName = nil
            toolDownloadProgress = nil
            toolDownloadTask = nil
        }
        let downloader = ToolDownloader(toolsDir: toolLocator().toolsDir)
        let task = Task { () -> URL in
            try await downloader.download(tool) { fraction in
                Task { @MainActor in
                    self.toolDownloadProgress = fraction
                }
            }
        }
        toolDownloadTask = task
        do {
            return try await task.value
        } catch {
            // A cancelled download is the user's own doing — no dialog.
            if !task.isCancelled {
                showError(error)
            }
            return nil
        }
    }

    /// Cancels an in-flight tool download (the progress sheet's Cancel and
    /// any dismissal of it).
    @MainActor func cancelToolDownload() {
        toolDownloadTask?.cancel()
    }

    // MARK: - Misc

    @MainActor func openReleasesPage() {
        if let url = URL(string: CRTVersion.releasesURL) {
            NSWorkspace.shared.open(url)
        }
    }

    @MainActor func showAbout() {
        Alerts.info(
            title: loc["About"],
            message: "Conner's Retime Tool v\(CRTVersion.current)\n\n"
                + "Created by Conner Glover\n\n"
                + "Credits:\nMenzo: French and Polish Translations\n"
                + "AmazinCris: Spanish Translations\n\n"
                + "© 2026 Conner Glover"
        )
    }
}
