import Foundation

/// One customizable hotkey action. Port of `src/crt/hotkeys/app.py` with
/// macOS defaults (Ctrl → Cmd) plus the new video-mode actions (spec §10).
public struct HotkeyAction: Sendable, Equatable, Identifiable {
    public let id: String
    public let labelKey: String
    public let defaultShortcut: String

    public init(id: String, labelKey: String, defaultShortcut: String) {
        self.id = id
        self.labelKey = labelKey
        self.defaultShortcut = defaultShortcut
    }
}

public enum HotkeyRegistry {

    public static let actions: [HotkeyAction] = [
        HotkeyAction(id: "New Time", labelKey: "New Time", defaultShortcut: "Cmd+N"),
        HotkeyAction(id: "Open Time", labelKey: "Open Time", defaultShortcut: "Cmd+O"),
        HotkeyAction(id: "Session History", labelKey: "Session History", defaultShortcut: "Cmd+H"),
        HotkeyAction(id: "Save", labelKey: "Save", defaultShortcut: "Cmd+S"),
        HotkeyAction(id: "Save As", labelKey: "Save As", defaultShortcut: "Cmd+Shift+S"),
        HotkeyAction(id: "Settings", labelKey: "Settings", defaultShortcut: "Cmd+,"),
        HotkeyAction(id: "Copy Mod Note", labelKey: "Copy Mod Note", defaultShortcut: "Cmd+M"),
        HotkeyAction(id: "Copy Discord Message", labelKey: "Copy Discord Message", defaultShortcut: "Cmd+Shift+D"),
        HotkeyAction(id: "Copy YouTube Chapters", labelKey: "Copy YouTube Chapters", defaultShortcut: "Cmd+Shift+Y"),
        HotkeyAction(id: "Clear Loads", labelKey: "Clear Loads", defaultShortcut: "Cmd+Shift+L"),
        HotkeyAction(id: "start_paste", labelKey: "Paste Start Frame", defaultShortcut: "Cmd+1"),
        HotkeyAction(id: "end_paste", labelKey: "Paste End Frame", defaultShortcut: "Cmd+2"),
        HotkeyAction(id: "start_loads_paste", labelKey: "Paste Start Frame (Loads)", defaultShortcut: "Cmd+3"),
        HotkeyAction(id: "end_loads_paste", labelKey: "Paste End Frame (Loads)", defaultShortcut: "Cmd+4"),
        HotkeyAction(id: "Add Loads", labelKey: "Add Load", defaultShortcut: "Cmd+L"),
        // New actions (spec §10)
        HotkeyAction(id: "Toggle Mode", labelKey: "Toggle Mode", defaultShortcut: "Cmd+T"),
        HotkeyAction(id: "video_frame_back", labelKey: "Video: Frame Back", defaultShortcut: ","),
        HotkeyAction(id: "video_frame_forward", labelKey: "Video: Frame Forward", defaultShortcut: "."),
        HotkeyAction(id: "video_play_pause", labelKey: "Video: Play/Pause", defaultShortcut: "Space"),
        HotkeyAction(id: "video_mark_start", labelKey: "Video: Mark Start", defaultShortcut: "["),
        HotkeyAction(id: "video_mark_end", labelKey: "Video: Mark End", defaultShortcut: "]"),
        HotkeyAction(id: "video_mark_load_start", labelKey: "Video: Mark Load Start", defaultShortcut: "L"),
        HotkeyAction(id: "video_mark_load_end", labelKey: "Video: Mark Load End", defaultShortcut: "Shift+L"),
    ]

    public static var defaults: [String: String] {
        var map: [String: String] = [:]
        for action in actions {
            map[action.id] = action.defaultShortcut
        }
        return map
    }

    /// Actions whose shortcut renders next to a menu entry.
    public static let menuActionIDs: Set<String> = [
        "New Time", "Open Time", "Session History", "Save", "Save As", "Settings",
        "Copy Mod Note", "Copy Discord Message", "Copy YouTube Chapters", "Clear Loads",
        "Toggle Mode",
    ]

    /// Converts an action id into a safe, stable INI option name —
    /// the exact `[^a-z0-9]+ → _` slug rule from the Python registry.
    public static func optionName(for actionID: String) -> String {
        var slug = ""
        var pendingSeparator = false
        for character in actionID.lowercased() {
            if (character >= "a" && character <= "z") || (character >= "0" && character <= "9") {
                if pendingSeparator && !slug.isEmpty {
                    slug.append("_")
                }
                pendingSeparator = false
                slug.append(character)
            } else {
                pendingSeparator = true
            }
        }
        return slug
    }

    /// Groups of action ids that share the same (non-empty) key combination.
    public static func duplicateGroups(in assignments: [String: String]) -> [[String]] {
        var byCombo: [String: [String]] = [:]
        for action in actions {
            let raw = assignments[action.id] ?? action.defaultShortcut
            guard let combo = KeyCombo(string: raw) else { continue }
            byCombo[combo.canonical, default: []].append(action.id)
        }
        return byCombo.values.filter { $0.count > 1 }.sorted { ($0.first ?? "") < ($1.first ?? "") }
    }
}

/// A parsed key combination such as "Cmd+Shift+S", "Shift+L" or ",".
public struct KeyCombo: Sendable, Equatable {
    public var command: Bool
    public var shift: Bool
    public var option: Bool
    public var control: Bool
    /// Normalized key: single characters lowercased ("s", ","), special keys
    /// as capitalized words ("Space", "Left", "Right", "Up", "Down", ...).
    public var key: String

    public init(command: Bool = false, shift: Bool = false, option: Bool = false,
                control: Bool = false, key: String) {
        self.command = command
        self.shift = shift
        self.option = option
        self.control = control
        self.key = key
    }

    public init?(string: String) {
        let parts = string.split(separator: "+", omittingEmptySubsequences: true)
            .map { $0.trimmingCharacters(in: .whitespaces) }
        var tokens = parts
        if parts.isEmpty, string.contains("+") {
            // A modifier-less "+" (or "++") — the separator IS the key. Without
            // this the canonical form "+" produced by `canonical` could never
            // be parsed back, silently killing the binding.
            tokens = ["+"]
        } else if string.hasSuffix("++") {
            // "Cmd++" style (a literal plus key) — the trailing empty part is "+".
            tokens.append("+")
        }
        guard !tokens.isEmpty else { return nil }

        var command = false
        var shift = false
        var option = false
        var control = false
        var key: String?

        for token in tokens {
            switch token.lowercased() {
            case "cmd", "command", "meta":
                command = true
            case "shift":
                shift = true
            case "option", "alt", "opt":
                option = true
            case "ctrl", "control":
                control = true
            default:
                key = KeyCombo.normalizeKey(token)
            }
        }

        guard let finalKey = key, !finalKey.isEmpty else { return nil }
        self.command = command
        self.shift = shift
        self.option = option
        self.control = control
        self.key = finalKey
    }

    static func normalizeKey(_ token: String) -> String {
        if token.count == 1 {
            return token.lowercased()
        }
        switch token.lowercased() {
        case "space":
            return "Space"
        case "left":
            return "Left"
        case "right":
            return "Right"
        case "up":
            return "Up"
        case "down":
            return "Down"
        case "return", "enter":
            return "Return"
        case "escape", "esc":
            return "Escape"
        case "tab":
            return "Tab"
        case "delete", "backspace":
            return "Delete"
        default:
            return token.capitalized
        }
    }

    /// Canonical string used for duplicate detection and storage.
    public var canonical: String {
        var parts: [String] = []
        if command { parts.append("Cmd") }
        if control { parts.append("Ctrl") }
        if option { parts.append("Option") }
        if shift { parts.append("Shift") }
        parts.append(key.count == 1 ? key.uppercased() : key)
        return parts.joined(separator: "+")
    }

    /// Human-readable display (same as canonical).
    public var display: String { canonical }
}
