import AppKit
import SwiftUI
import CRTCore

/// Maps a stored key combination onto a SwiftUI menu shortcut.
func menuShortcut(for actionID: String) -> KeyboardShortcut? {
    let stored = AppModel.shared.settings.hotkey(for: actionID)
    guard let combo = KeyCombo(string: stored) else { return nil }

    var modifiers: EventModifiers = []
    if combo.command { modifiers.insert(.command) }
    if combo.shift { modifiers.insert(.shift) }
    if combo.option { modifiers.insert(.option) }
    if combo.control { modifiers.insert(.control) }

    let key: KeyEquivalent
    switch combo.key.lowercased() {
    case "space": key = .space
    case "left": key = .leftArrow
    case "right": key = .rightArrow
    case "up": key = .upArrow
    case "down": key = .downArrow
    case "return": key = .return
    case "escape": key = .escape
    case "tab": key = .tab
    case "delete": key = .delete
    case "home": key = .home
    case "end": key = .end
    case "pageup": key = .pageUp
    case "pagedown": key = .pageDown
    case "clear": key = .clear
    default:
        // Function keys: AppKit expresses them as private-use scalars
        // (F1 = U+F704), which is what a menu key equivalent expects.
        if combo.key.count > 1, combo.key.hasPrefix("F") || combo.key.hasPrefix("f"),
           let number = Int(combo.key.dropFirst()), (1...12).contains(number),
           let scalar = Unicode.Scalar(UInt32(0xF704 + number - 1)) {
            key = KeyEquivalent(Character(scalar))
        } else {
            guard combo.key.count == 1, let character = combo.key.first else { return nil }
            key = KeyEquivalent(character)
        }
    }
    return KeyboardShortcut(key, modifiers: modifiers)
}

/// Native menu bar (spec §6). Actions hop to the main actor explicitly since
/// command closures are not statically isolated.
struct CRTCommands: Commands {

    private var loc: Localization { AppModel.shared.loc }

    var body: some Commands {
        // File
        CommandGroup(replacing: .newItem) {
            Button(loc["New Time"]) {
                runOnMain { AppModel.shared.newTime() }
            }
            .keyboardShortcut(menuShortcut(for: "New Time"))

            Button(loc["Open Time"] + "…") {
                runOnMain { AppModel.shared.openTime() }
            }
            .keyboardShortcut(menuShortcut(for: "Open Time"))

            Button(loc["Session History"] + "…") {
                runOnMain { AppModel.shared.showHistorySheet = true }
            }
            .keyboardShortcut(menuShortcut(for: "Session History"))

            Divider()

            Button(loc["Save"]) {
                runOnMain { AppModel.shared.saveTime() }
            }
            .keyboardShortcut(menuShortcut(for: "Save"))

            Button(loc["Save As"] + "…") {
                runOnMain { AppModel.shared.saveAsTime() }
            }
            .keyboardShortcut(menuShortcut(for: "Save As"))
        }

        // Edit additions (copy actions + clear loads + toggle mode)
        CommandGroup(after: .pasteboard) {
            Divider()

            Button(loc["Copy Mod Note"]) {
                runOnMain { AppModel.shared.copyModNote() }
            }
            .keyboardShortcut(menuShortcut(for: "Copy Mod Note"))

            Button(loc["Copy Discord Message"]) {
                runOnMain { AppModel.shared.copyDiscordMessage() }
            }
            .keyboardShortcut(menuShortcut(for: "Copy Discord Message"))

            Button(loc["Copy YouTube Chapters"]) {
                runOnMain { AppModel.shared.copyYouTubeChapters() }
            }
            .keyboardShortcut(menuShortcut(for: "Copy YouTube Chapters"))

            Button(loc["Copy Time Without Loads"]) {
                runOnMain { AppModel.shared.copyTimeWithoutLoads() }
            }
            .keyboardShortcut(KeyboardShortcut("c", modifiers: [.command, .shift]))

            Divider()

            Button(loc["Clear Loads"]) {
                runOnMain { AppModel.shared.clearMarks() }
            }
            .keyboardShortcut(menuShortcut(for: "Clear Loads"))

            Button(loc["Toggle Mode"]) {
                runOnMain { AppModel.shared.toggleMode() }
            }
            .keyboardShortcut(menuShortcut(for: "Toggle Mode"))

            // Registry actions with no menu of their own (port of the
            // standalone QShortcuts in `App._apply_hotkeys`, spec §10).
            // Grouped to stay inside the builder's child limit.
            Group {
                Divider()

                Button(loc["Add Load"]) {
                    runOnMain {
                        let model = AppModel.shared
                        if model.files.session.mode == .segments {
                            model.addSegment()
                        } else {
                            model.addLoads()
                        }
                    }
                }
                .keyboardShortcut(menuShortcut(for: "Add Loads"))

                Button(loc["Paste Start Frame"]) {
                    runOnMain { AppModel.shared.paste(into: .start) }
                }
                .keyboardShortcut(menuShortcut(for: "start_paste"))

                Button(loc["Paste End Frame"]) {
                    runOnMain { AppModel.shared.paste(into: .end) }
                }
                .keyboardShortcut(menuShortcut(for: "end_paste"))

                Button(loc["Paste Start Frame (Loads)"]) {
                    runOnMain {
                        let model = AppModel.shared
                        model.paste(into: model.files.session.mode == .segments ? .segStart : .loadStart)
                    }
                }
                .keyboardShortcut(menuShortcut(for: "start_loads_paste"))

                Button(loc["Paste End Frame (Loads)"]) {
                    runOnMain {
                        let model = AppModel.shared
                        model.paste(into: model.files.session.mode == .segments ? .segEnd : .loadEnd)
                    }
                }
                .keyboardShortcut(menuShortcut(for: "end_loads_paste"))
            }
        }

        // Undo/redo on the session snapshot stack (spec §14)
        CommandGroup(replacing: .undoRedo) {
            Button(loc["Undo"]) {
                runOnMain { AppModel.shared.undo() }
            }
            .keyboardShortcut(KeyboardShortcut("z", modifiers: [.command]))
            .disabled(!AppModel.shared.canUndo)

            Button(loc["Redo"]) {
                runOnMain { AppModel.shared.redo() }
            }
            .keyboardShortcut(KeyboardShortcut("z", modifiers: [.command, .shift]))
            .disabled(!AppModel.shared.canRedo)
        }

        // Settings — replaces the framework-generated item so the shortcut
        // comes from the hotkey registry (spec §6 / §10).
        CommandGroup(replacing: .appSettings) {
            SettingsLink {
                Text(loc["Settings"] + "…")
            }
            .keyboardShortcut(menuShortcut(for: "Settings"))
        }

        // View — Always on Top (ON by default, spec §6)
        CommandGroup(after: .sidebar) {
            Toggle(loc["Always on Top"], isOn: Binding(
                get: { AppModel.shared.alwaysOnTop },
                set: { newValue in
                    runOnMain { AppModel.shared.setAlwaysOnTop(newValue) }
                }
            ))
        }

        // About
        CommandGroup(replacing: .appInfo) {
            Button(loc["About"] + " CRT") {
                runOnMain { AppModel.shared.showAbout() }
            }
        }

        // Help
        CommandGroup(replacing: .help) {
            Button("CRT " + loc["Help"]) {
                runOnMain {
                    if let url = URL(string: CRTVersion.repositoryURL) {
                        NSWorkspace.shared.open(url)
                    }
                }
            }
        }
    }
}
