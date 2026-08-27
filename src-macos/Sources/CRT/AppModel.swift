import AppKit
import Foundation
import Observation
import CRTCore

enum AppPage: String, Hashable, CaseIterable {
    case dashboard
    case frameRetimer
    case videoRetimer
}

enum FrameField {
    case start
    case end
    case loadStart
    case loadEnd
    case segStart
    case segEnd
}

/// Root application model. State mutations happen on the main actor (all
/// entry points are UI-driven); AppKit-touching methods are marked @MainActor.
@Observable
@MainActor
final class AppModel {
    static let shared = AppModel()

    // MARK: Services (immutable, not observed)
    let settings: SettingsService
    let loc: Localization
    let recents: RecentFilesStore
    let libraryStore: RunLibraryStore
    let autosave: AutosaveService
    let video: VideoModel
    let src: SpeedrunModel

    // MARK: Observable state
    var files = SessionFileManager()
    var page: AppPage = .dashboard
    var alwaysOnTop = true
    var updateVersion: String?
    var toastMessage: String?
    var sidebarCollapsed = false
    var libraryEntries: [RunLibraryEntry] = []
    var recentPaths: [String] = []
    var showHistorySheet = false
    var busyLookup = false
    var toolDownloadName: String?
    var toolDownloadProgress: Double?

    /// Observed mirror of `settings.accent_color` — `SettingsService` is not
    /// observable, so the tint needs an observed source to update on Apply.
    var accentColorHex: String = "#5b9bd5"

    // Input field texts (committed on Enter / focus loss)
    var framerateText = "60"
    var startText = "0"
    var endText = "0"
    var loadStartText = "0"
    var loadEndText = "0"
    var segStartText = "0"
    var segEndText = "0"

    // Undo / redo (spec §14)
    var undoStack: [TimeSession] = []
    var redoStack: [TimeSession] = []

    // MARK: Non-observed bookkeeping
    @ObservationIgnored var framerateMismatchSeen: Set<String> = []
    @ObservationIgnored weak var mainWindow: NSWindow?
    @ObservationIgnored var toastTask: Task<Void, Never>?
    @ObservationIgnored var autosaveTask: Task<Void, Never>?
    @ObservationIgnored var toolDownloadTask: Task<URL, Error>?
    @ObservationIgnored var launched = false

    init(settings: SettingsService = SettingsService()) {
        self.settings = settings
        self.loc = Localization(language: settings.language)
        self.accentColorHex = settings.accentColorHex
        self.recents = RecentFilesStore(configDir: settings.configDir)
        self.libraryStore = RunLibraryStore(configDir: settings.configDir)
        self.autosave = AutosaveService(configDir: settings.configDir)
        self.video = VideoModel()
        self.src = SpeedrunModel()
        self.video.app = self
        self.src.app = self
    }

    // MARK: - Session access & undo

    var session: TimeSession {
        files.session
    }

    func toolLocator() -> ToolLocator {
        ToolLocator(configDir: settings.configDir,
                    ffmpegOverride: settings.ffmpegPath,
                    ytDlpOverride: settings.ytDlpPath)
    }

    func clearUndo() {
        undoStack = []
        redoStack = []
    }

    func pushUndo() {
        undoStack.append(files.session)
        if undoStack.count > 200 {
            undoStack.removeFirst()
        }
        redoStack = []
    }

    var canUndo: Bool { !undoStack.isEmpty }
    var canRedo: Bool { !redoStack.isEmpty }

    @MainActor func undo() {
        guard let previous = undoStack.popLast() else { return }
        redoStack.append(files.session)
        files.session = previous
        files.dirty = true
        syncInputs()
    }

    @MainActor func redo() {
        guard let next = redoStack.popLast() else { return }
        undoStack.append(files.session)
        files.session = next
        files.dirty = true
        syncInputs()
    }

    /// Snapshot + mutate + mark dirty.
    func mutateSession(_ change: (inout TimeSession) -> Void) {
        pushUndo()
        change(&files.session)
        files.dirty = true
    }

    /// Throwing variant; rolls the undo snapshot back on failure.
    func mutateSessionThrowing(_ change: (inout TimeSession) throws -> Void) throws {
        pushUndo()
        do {
            try change(&files.session)
            files.dirty = true
        } catch {
            _ = undoStack.popLast()
            throw error
        }
    }

    // MARK: - Errors & toasts

    @MainActor func showError(_ error: Error) {
        let message = (error as? CRTError)?.messageText ?? error.localizedDescription
        Alerts.error(title: loc["Error"], message: message)
    }

    @MainActor func showErrorMessage(_ message: String) {
        Alerts.error(title: loc["Error"], message: message)
    }

    @MainActor func toast(_ message: String) {
        toastTask?.cancel()
        toastMessage = message
        toastTask = Task { @MainActor in
            try? await Task.sleep(nanoseconds: 2_500_000_000)
            if !Task.isCancelled {
                self.toastMessage = nil
            }
        }
    }

    // MARK: - Input commits (spec §2, §6)

    func text(for field: FrameField) -> String {
        switch field {
        case .start: return startText
        case .end: return endText
        case .loadStart: return loadStartText
        case .loadEnd: return loadEndText
        case .segStart: return segStartText
        case .segEnd: return segEndText
        }
    }

    @MainActor func commitFramerate() {
        let framerate = FrameInputParser.cleanFramerate(framerateText)
        framerateText = TimeFormatter.string(framerate)
        if files.session.framerate != framerate {
            mutateSession { $0.framerate = framerate }
        }
    }

    @MainActor func setFramerate(_ fps: Decimal, announce: Bool) {
        if files.session.framerate != fps {
            mutateSession { $0.framerate = fps }
        }
        framerateText = TimeFormatter.string(fps)
        if announce {
            toast(loc.text("Framerate set from video", "Framerate set to {fps} FPS")
                .replacingOccurrences(of: "{fps}", with: TimeFormatter.string(fps)))
        }
    }

    /// Commits a frame field: parse (with the §2.1 mismatch check for pasted
    /// debug info) and replace the field text with the parsed frame number.
    @MainActor func commitFrameField(_ field: FrameField) {
        let currentText = text(for: field)
        Task { @MainActor in
            await self.commitFrameFieldAsync(field, text: currentText)
        }
    }

    @MainActor func paste(into field: FrameField) {
        let clipboardText = Clipboard.get()
        Task { @MainActor in
            await self.commitFrameFieldAsync(field, text: clipboardText)
        }
    }

    @MainActor func commitFrameFieldAsync(_ field: FrameField, text inputText: String) async {
        do {
            let frame = try await parseFrame(inputText)
            switch field {
            case .start:
                if files.session.startFrame != frame {
                    mutateSession { $0.startFrame = frame }
                }
                startText = String(frame)
            case .end:
                if files.session.endFrame != frame {
                    mutateSession { $0.endFrame = frame }
                }
                endText = String(frame)
            case .loadStart:
                loadStartText = String(frame)
            case .loadEnd:
                loadEndText = String(frame)
            case .segStart:
                segStartText = String(frame)
            case .segEnd:
                segEndText = String(frame)
            }
        } catch {
            showError(error)
            switch field {
            case .loadStart: loadStartText = "0"
            case .loadEnd: loadEndText = "0"
            case .segStart: segStartText = "0"
            case .segEnd: segEndText = "0"
            case .start, .end: break
            }
        }
    }

    @MainActor func parseFrame(_ inputText: String) async throws -> Int {
        if FrameInputParser.isDebugInfo(inputText) {
            await confirmFramerateFromDebugInfo(inputText)
        }
        return try FrameInputParser.parse(inputText, framerate: files.session.framerate)
    }

    /// Spec §2.1 — offer to correct the session framerate before converting
    /// pasted YouTube debug info. Asks at most once per (video, itag).
    @MainActor func confirmFramerateFromDebugInfo(_ debugInfo: String) async {
        guard let ids = FrameInputParser.extractDebugInfoIDs(debugInfo) else { return }
        let key = ids.videoID + "|" + ids.formatID
        guard !framerateMismatchSeen.contains(key) else { return }

        busyLookup = true
        var detected = await InnertubeClient.shared.framerate(videoID: ids.videoID, formatID: ids.formatID)
        if detected == nil, let ytDlp = toolLocator().locate(.ytDlp) {
            let watchURL = "https://www.youtube.com/watch?v=\(ids.videoID)"
            if let data = try? await YtDlpImporter().fetchInfoJSON(url: watchURL, ytDlp: ytDlp) {
                detected = InnertubeClient.parseYtDlpInfo(data, formatID: ids.formatID)
            }
        }
        busyLookup = false

        guard let detectedFps = detected else { return }
        let current = files.session.framerate
        if abs(detectedFps - current) < 1 { return }

        framerateMismatchSeen.insert(key)
        let message = loc.text(
            "Framerate Mismatch Message",
            "This video appears to be {detected} FPS, but the session is set to {current} FPS.\n\n"
                + "Update the framerate before calculating the frame?"
        )
        .replacingOccurrences(of: "{detected}", with: TimeFormatter.string(detectedFps))
        .replacingOccurrences(of: "{current}", with: TimeFormatter.string(current))
        if Alerts.confirmYesNo(title: loc["Framerate Mismatch"], message: message) {
            setFramerate(detectedFps, announce: false)
        }
    }

    func syncInputs() {
        let current = files.session
        framerateText = TimeFormatter.string(current.framerate)
        startText = String(current.startFrame)
        endText = String(current.endFrame)
        loadStartText = "0"
        loadEndText = "0"
        segStartText = "0"
        segEndText = "0"
    }

    // MARK: - Loads / segments

    /// Adds a load from the two input fields. The fields are §2 frame inputs
    /// (seconds, stripped junk or a pasted YouTube debug-info blob are all
    /// legal), so they go through the same parser as a commit — a button
    /// click does not move focus on macOS, so the fields are usually still
    /// uncommitted at this point.
    @MainActor func addLoads() {
        let startInput = loadStartText
        let endInput = loadEndText
        Task { @MainActor in
            do {
                let start = try await self.parseFrame(startInput)
                let end = try await self.parseFrame(endInput)

                if self.concerningLoad(startFrame: start, endFrame: end) {
                    let confirmed = Alerts.confirmYesNo(
                        title: self.loc["Woah!"],
                        message: self.loc.text(
                            "Concerningly Long Load Message",
                            "This load is concerningly long. Would you like to add the load anyway?"
                        )
                    )
                    if !confirmed { return }
                }

                try self.mutateSessionThrowing { try $0.addLoad(startFrame: start, endFrame: end) }
                self.loadStartText = "0"
                self.loadEndText = "0"
                self.toast(self.loc["Load added successfully."])
            } catch {
                self.showError(error)
            }
        }
    }

    /// Spec §1 / `_add_loads`: fires when the new load is longer than ten
    /// times the average existing load. Python divides in floating point, so
    /// the comparison is cross-multiplied here instead of using the
    /// truncating integer average.
    @MainActor private func concerningLoad(startFrame: Int, endFrame: Int) -> Bool {
        let loads = files.session.loads
        guard !loads.isEmpty else { return false }
        let totalLength = loads.reduce(0) { $0 + $1.length }
        return (endFrame - startFrame) * loads.count > totalLength * 10
    }

    @MainActor func addSegment() {
        let startInput = segStartText
        let endInput = segEndText
        Task { @MainActor in
            do {
                let start = try await self.parseFrame(startInput)
                let end = try await self.parseFrame(endInput)
                try self.mutateSessionThrowing { try $0.addSegment(startFrame: start, endFrame: end) }
                self.segStartText = "0"
                self.segEndText = "0"
                self.toast(self.loc["Segment added successfully."])
            } catch {
                self.showError(error)
            }
        }
    }

    /// Inline sidebar edit (mode-aware). Parses both fields through the §2
    /// pipeline, like the Python `_on_load_edited`.
    @MainActor func editMark(at index: Int, startText markStart: String, endText markEnd: String) {
        Task { @MainActor in
            do {
                let start = try await self.parseFrame(markStart)
                let end = try await self.parseFrame(markEnd)
                try self.mutateSessionThrowing { session in
                    if session.mode == .segments {
                        try session.mutateSegment(at: index, startFrame: start, endFrame: end)
                    } else {
                        try session.mutateLoad(at: index, startFrame: start, endFrame: end)
                    }
                }
            } catch {
                self.showError(error)
            }
        }
    }

    @MainActor func deleteMark(at index: Int) {
        mutateSession { session in
            if session.mode == .segments {
                session.deleteSegment(at: index)
            } else {
                session.deleteLoad(at: index)
            }
        }
    }

    @MainActor func clearMarks() {
        mutateSession { session in
            if session.mode == .segments {
                session.clearSegments()
            } else {
                session.clearLoads()
            }
        }
    }

    @MainActor func toggleMode() {
        mutateSession { $0.mode = $0.mode == .loads ? .segments : .loads }
        toast(files.session.mode == .segments ? loc["Segment Mode"] : loc["Load Mode"])
    }

    // MARK: - Copy actions (spec §5)

    func currentModNote() -> String {
        ModNoteBuilder.build(template: settings.modNoteFormat, session: files.session)
    }

    @MainActor func copyModNote() {
        Clipboard.set(currentModNote())
        toast(loc["Mod note copied"])
    }

    @MainActor func copyDiscordMessage() {
        Clipboard.set(CopyFormats.discordMessage(session: files.session))
        toast(loc["Discord message copied"])
    }

    @MainActor func copyYouTubeChapters() {
        Clipboard.set(CopyFormats.youtubeChapters(session: files.session))
        toast(loc["YouTube chapters copied"])
    }

    @MainActor func copyTimeWithoutLoads() {
        Clipboard.set(files.session.isoWithoutLoads())
        toast(loc["Time copied"])
    }

    @MainActor func copyTimeWithLoads() {
        Clipboard.set(files.session.isoWithLoads())
        toast(loc["Time copied"])
    }
}
