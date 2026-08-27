import AppKit
import SwiftUI
import CRTCore

/// The hotkey editor (spec §10): one row per action with live capture,
/// per-row Reset, Reset All, and duplicate detection.
@MainActor
struct HotkeyEditorView: View {
    /// Owns the local key monitor so it is torn down even when the settings
    /// window disappears without `onDisappear` running — a live monitor would
    /// otherwise swallow the next keystroke anywhere in the app and silently
    /// rebind an action.
    final class Recorder {
        var monitor: Any?

        func stop() {
            if let monitor {
                NSEvent.removeMonitor(monitor)
            }
            monitor = nil
        }

        deinit {
            if let monitor {
                NSEvent.removeMonitor(monitor)
            }
        }
    }

    @Environment(AppModel.self) private var model
    @Binding var hotkeys: [String: String]

    @State private var recordingActionID: String?
    @State private var recorder = Recorder()

    var body: some View {
        VStack(spacing: 0) {
            List {
                ForEach(HotkeyRegistry.actions) { action in
                    row(for: action)
                }
            }

            HStack {
                Button(model.loc["Reset All"]) {
                    endRecording()
                    hotkeys = HotkeyRegistry.defaults
                }
                Spacer()
                Text(model.loc.text(
                    "Hotkey Capture Hint",
                    "Click a shortcut, then press the new key combination. Esc cancels."
                ))
                .font(.caption)
                .foregroundStyle(.secondary)
            }
            .padding(12)
        }
        .onDisappear {
            endRecording()
        }
    }

    private func row(for action: HotkeyAction) -> some View {
        HStack {
            Text(model.loc[action.labelKey])
            Spacer()

            Button {
                beginRecording(action.id)
            } label: {
                Text(recordingActionID == action.id
                     ? model.loc["Press a Key Combination"]
                     : (hotkeys[action.id] ?? action.defaultShortcut))
                    .font(.callout.monospaced())
                    .frame(minWidth: 130)
            }
            .buttonStyle(.bordered)
            .tint(rowTint(for: action))

            Button(model.loc["Reset"]) {
                endRecording()
                hotkeys[action.id] = action.defaultShortcut
            }
            .controlSize(.small)
        }
        .padding(.vertical, 2)
    }

    /// Action ids sharing a combination — flagged in red so the user can see
    /// which rows Apply will refuse.
    private var duplicateIDs: Set<String> {
        Set(HotkeyRegistry.duplicateGroups(in: hotkeys).flatMap { $0 })
    }

    private func rowTint(for action: HotkeyAction) -> Color? {
        if recordingActionID == action.id { return .accentColor }
        if duplicateIDs.contains(action.id) { return .red }
        return nil
    }

    // MARK: - Capture (NSEvent local monitor)

    private func beginRecording(_ actionID: String) {
        endRecording()
        recordingActionID = actionID
        recorder.monitor = NSEvent.addLocalMonitorForEvents(matching: [.keyDown]) { event in
            handleCapture(event)
            return nil // swallow the event while recording
        }
    }

    private func endRecording() {
        recorder.stop()
        recordingActionID = nil
    }

    private func handleCapture(_ event: NSEvent) {
        guard let actionID = recordingActionID else {
            endRecording()
            return
        }
        // Esc cancels the capture.
        if event.keyCode == 53 {
            endRecording()
            return
        }
        guard let combo = HotkeyEditorView.combo(from: event) else { return }
        hotkeys[actionID] = combo.canonical
        endRecording()
        warnAboutDuplicates()
    }

    static func combo(from event: NSEvent) -> KeyCombo? {
        let flags = event.modifierFlags
        let key: String
        switch event.keyCode {
        case 49: key = "Space"
        case 123: key = "Left"
        case 124: key = "Right"
        case 125: key = "Down"
        case 126: key = "Up"
        case 36, 76: key = "Return"
        case 48: key = "Tab"
        case 51, 117: key = "Delete"
        // Function and navigation keys: AppKit reports these as private-use
        // codepoints in `charactersIgnoringModifiers`, which would be written
        // verbatim into settings.ini (spec §4 keeps that file readable).
        case 122: key = "F1"
        case 120: key = "F2"
        case 99: key = "F3"
        case 118: key = "F4"
        case 96: key = "F5"
        case 97: key = "F6"
        case 98: key = "F7"
        case 100: key = "F8"
        case 101: key = "F9"
        case 109: key = "F10"
        case 103: key = "F11"
        case 111: key = "F12"
        case 115: key = "Home"
        case 119: key = "End"
        case 116: key = "PageUp"
        case 121: key = "PageDown"
        case 71: key = "Clear"
        default:
            guard let characters = event.charactersIgnoringModifiers,
                  let first = characters.unicodeScalars.first else {
                return nil
            }
            // Ignore any remaining private-use scalar rather than persisting it.
            if (0xF700...0xF8FF).contains(first.value) {
                return nil
            }
            key = String(Character(first)).lowercased()
        }
        return KeyCombo(
            command: flags.contains(.command),
            shift: flags.contains(.shift),
            option: flags.contains(.option),
            control: flags.contains(.control),
            key: key
        )
    }

    /// The "Duplicate Hotkey Message" text for a set of conflicting groups.
    static func duplicateMessage(_ groups: [[String]], loc: Localization) -> String {
        let names = groups
            .map { group in group.map { id in
                loc[HotkeyRegistry.actions.first(where: { $0.id == id })?.labelKey ?? id]
            }.joined(separator: ", ") }
            .joined(separator: "; ")
        return loc["Duplicate Hotkey Message"].replacingOccurrences(of: "{names}", with: names)
    }

    private func warnAboutDuplicates() {
        let groups = HotkeyRegistry.duplicateGroups(in: hotkeys)
        guard !groups.isEmpty else { return }
        let message = HotkeyEditorView.duplicateMessage(groups, loc: model.loc)
        let title = model.loc["Duplicate Hotkey"]
        // Runs the modal after event dispatch unwinds — this is reached from
        // inside the local key monitor's handler.
        Task { @MainActor in
            Alerts.info(title: title, message: message)
        }
    }
}
