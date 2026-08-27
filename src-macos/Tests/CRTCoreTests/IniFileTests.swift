import XCTest
@testable import CRTCore

final class IniFileTests: XCTestCase {

    private func tempDir() throws -> URL {
        let url = FileManager.default.temporaryDirectory
            .appendingPathComponent("crt-ini-tests-\(UUID().uuidString)", isDirectory: true)
        try FileManager.default.createDirectory(at: url, withIntermediateDirectories: true)
        return url
    }

    // MARK: - Parsing (ConfigParser compatibility)

    func testParseBasics() {
        let text = """
        [Settings]
        Theme = Automatic
        accent_color = #5b9bd5

        [Hotkeys]
        new_time = Cmd+N
        """
        let ini = IniFile(text: text)
        XCTAssertEqual(ini.get("Settings", "theme"), "Automatic")
        XCTAssertEqual(ini.get("Settings", "THEME"), "Automatic")
        XCTAssertEqual(ini.get("Settings", "accent_color"), "#5b9bd5")
        XCTAssertEqual(ini.get("Hotkeys", "new_time"), "Cmd+N")
        XCTAssertNil(ini.get("Hotkeys", "missing"))
        XCTAssertNil(ini.get("Nope", "theme"))
    }

    func testValueMayContainDelimiters() {
        let ini = IniFile(text: "[Settings]\nmod_note_format = Retimed to {t} = nice: yes\n")
        XCTAssertEqual(ini.get("Settings", "mod_note_format"), "Retimed to {t} = nice: yes")
    }

    func testColonDelimiter() {
        let ini = IniFile(text: "[S]\nkey: value\n")
        XCTAssertEqual(ini.get("S", "key"), "value")
    }

    func testCommentsAndBlanksIgnored() {
        let text = """
        # comment
        ; also comment
        [S]

        a = 1
        """
        let ini = IniFile(text: text)
        XCTAssertEqual(ini.get("S", "a"), "1")
    }

    // MARK: - Round trip

    func testSerializedRoundTrip() {
        var ini = IniFile()
        ini.set("Settings", "enable_updates", "True")
        ini.set("Settings", "mod_note_format", "Mod Note: Retimed to {time_without_loads}")
        ini.set("Hotkeys", "save_as", "Cmd+Shift+S")

        let reparsed = IniFile(text: ini.serialized())
        XCTAssertEqual(reparsed, ini)
        XCTAssertEqual(reparsed.get("Hotkeys", "save_as"), "Cmd+Shift+S")
    }

    func testGetBool() {
        var ini = IniFile()
        ini.set("S", "a", "True")
        ini.set("S", "b", "off")
        ini.set("S", "c", "banana")
        XCTAssertEqual(ini.getBool("S", "a"), true)
        XCTAssertEqual(ini.getBool("S", "b"), false)
        XCTAssertNil(ini.getBool("S", "c"))
    }

    // MARK: - SettingsService sync (spec §4)

    func testSettingsServiceCreatesDefaults() throws {
        let dir = try tempDir()
        defer { try? FileManager.default.removeItem(at: dir) }

        let service = SettingsService(configDir: dir)
        XCTAssertTrue(FileManager.default.fileExists(atPath: service.fileURL.path))
        XCTAssertTrue(service.enableUpdates)
        XCTAssertEqual(service.theme, "Automatic")
        XCTAssertEqual(service.accentColorHex, "#5b9bd5")
        XCTAssertEqual(service.language, "en")
        XCTAssertEqual(service.modNoteFormat, "Mod Note: Retimed to {time_without_loads}")
        XCTAssertEqual(service.timerCorner, "bottom-right")
        XCTAssertEqual(service.timerStyle, "pill")
        XCTAssertEqual(service.defaultMode, "loads")
        XCTAssertEqual(service.hotkey(for: "New Time"), "Cmd+N")
        XCTAssertEqual(service.hotkey(for: "video_mark_load_end"), "Shift+L")
    }

    func testSettingsServiceSyncsMissingKeys() throws {
        let dir = try tempDir()
        defer { try? FileManager.default.removeItem(at: dir) }

        // A partial (Python-era) file: only some keys, no video keys.
        let partial = """
        [Settings]
        theme = Dark
        language = Français
        """
        let fileURL = dir.appendingPathComponent("settings.ini")
        try partial.write(to: fileURL, atomically: true, encoding: .utf8)

        let service = SettingsService(configDir: dir)
        // Existing values kept…
        XCTAssertEqual(service.theme, "Dark")
        XCTAssertEqual(service.language, "Français")
        // …missing ones synced with defaults and written back.
        XCTAssertEqual(service.timerStyle, "pill")
        XCTAssertEqual(service.hotkey(for: "Save"), "Cmd+S")

        let written = try String(contentsOf: fileURL, encoding: .utf8)
        XCTAssertTrue(written.contains("timer_style = pill"))
        XCTAssertTrue(written.contains("[Hotkeys]"))
    }

    func testSettingsServicePersistsChanges() throws {
        let dir = try tempDir()
        defer { try? FileManager.default.removeItem(at: dir) }

        let service = SettingsService(configDir: dir)
        service.setValue("theme", "Light")
        service.setHotkey("Save", "Cmd+Option+S")
        try service.save()

        let reloaded = SettingsService(configDir: dir)
        XCTAssertEqual(reloaded.theme, "Light")
        XCTAssertEqual(reloaded.hotkey(for: "Save"), "Cmd+Option+S")
    }

    func testRestoreDefaults() throws {
        let dir = try tempDir()
        defer { try? FileManager.default.removeItem(at: dir) }

        let service = SettingsService(configDir: dir)
        service.setValue("theme", "Dark")
        try service.save()
        service.restoreDefaults()
        XCTAssertEqual(service.theme, "Automatic")

        let reloaded = SettingsService(configDir: dir)
        XCTAssertEqual(reloaded.theme, "Automatic")
    }
}
