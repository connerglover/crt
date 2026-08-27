import Foundation

/// Application settings backed by a ConfigParser-compatible `settings.ini`
/// (spec §4). Path on macOS: `~/Library/Application Support/CRT/settings.ini`.
public final class SettingsService {

    /// Ordered defaults for `[Settings]` (order matters for a tidy file).
    public static let defaults: [(key: String, value: String)] = [
        ("enable_updates", "True"),
        ("theme", "Automatic"),
        ("accent_color", "#5b9bd5"),
        ("language", "en"),
        ("mod_note_format", "Mod Note: Retimed to {time_without_loads}"),
        // Native-only keys, synced the same way.
        ("timer_corner", "bottom-right"),
        ("timer_style", "pill"),
        ("ffmpeg_path", ""),
        ("ytdlp_path", ""),
        ("default_mode", "loads"),
    ]

    public let configDir: URL
    public let fileURL: URL
    public private(set) var ini: IniFile

    /// Resolves `~/Library/Application Support/CRT`.
    public static func defaultConfigDir() -> URL {
        let base = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask).first
            ?? FileManager.default.homeDirectoryForCurrentUser
                .appendingPathComponent("Library")
                .appendingPathComponent("Application Support")
        return base.appendingPathComponent("CRT", isDirectory: true)
    }

    public init(configDir: URL) {
        self.configDir = configDir
        self.fileURL = configDir.appendingPathComponent("settings.ini")

        try? FileManager.default.createDirectory(at: configDir, withIntermediateDirectories: true)

        if let existing = try? IniFile.read(from: fileURL) {
            self.ini = existing
        } else {
            self.ini = IniFile()
        }

        syncMissing()
    }

    public convenience init() {
        self.init(configDir: SettingsService.defaultConfigDir())
    }

    /// Adds any missing keys/sections with defaults and rewrites the file if
    /// anything changed (port of `_sync_missing_settings` / `_sync_missing_hotkeys`).
    public func syncMissing() {
        var changed = false

        if !ini.hasSection("Settings") {
            ini.addSection("Settings")
            changed = true
        }
        for (key, value) in SettingsService.defaults {
            if !ini.hasOption("Settings", key) {
                ini.set("Settings", key, value)
                changed = true
            }
        }

        if !ini.hasSection("Hotkeys") {
            ini.addSection("Hotkeys")
            changed = true
        }
        for action in HotkeyRegistry.actions {
            let option = HotkeyRegistry.optionName(for: action.id)
            if !ini.hasOption("Hotkeys", option) {
                ini.set("Hotkeys", option, action.defaultShortcut)
                changed = true
            }
        }

        if changed {
            try? ini.write(to: fileURL)
        }
    }

    // MARK: - Values

    public func value(_ key: String) -> String {
        if let stored = ini.get("Settings", key) {
            return stored
        }
        return SettingsService.defaults.first(where: { $0.key == key })?.value ?? ""
    }

    public func setValue(_ key: String, _ newValue: String) {
        ini.set("Settings", key, newValue)
    }

    public var enableUpdates: Bool {
        ini.getBool("Settings", "enable_updates") ?? true
    }

    public var theme: String { value("theme") }
    public var accentColorHex: String { value("accent_color") }
    public var language: String { value("language") }
    public var modNoteFormat: String { value("mod_note_format") }
    public var timerCorner: String { value("timer_corner") }
    public var timerStyle: String { value("timer_style") }
    public var ffmpegPath: String { value("ffmpeg_path") }
    public var ytDlpPath: String { value("ytdlp_path") }
    public var defaultMode: String { value("default_mode") }

    // MARK: - Hotkeys

    public func hotkey(for actionID: String) -> String {
        let option = HotkeyRegistry.optionName(for: actionID)
        if let stored = ini.get("Hotkeys", option) {
            return stored
        }
        return HotkeyRegistry.defaults[actionID] ?? ""
    }

    public func setHotkey(_ actionID: String, _ shortcut: String) {
        ini.set("Hotkeys", HotkeyRegistry.optionName(for: actionID), shortcut)
    }

    public func allHotkeys() -> [String: String] {
        var map: [String: String] = [:]
        for action in HotkeyRegistry.actions {
            map[action.id] = hotkey(for: action.id)
        }
        return map
    }

    // MARK: - Persistence

    public func save() throws {
        try ini.write(to: fileURL)
    }

    /// Restores every setting and hotkey to defaults and writes the file.
    public func restoreDefaults() {
        var fresh = IniFile()
        fresh.addSection("Settings")
        for (key, value) in SettingsService.defaults {
            fresh.set("Settings", key, value)
        }
        fresh.addSection("Hotkeys")
        for action in HotkeyRegistry.actions {
            fresh.set("Hotkeys", HotkeyRegistry.optionName(for: action.id), action.defaultShortcut)
        }
        ini = fresh
        try? ini.write(to: fileURL)
    }
}
