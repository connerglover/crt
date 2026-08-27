import XCTest
@testable import CRTCore

final class HotkeyRegistryTests: XCTestCase {

    // MARK: - Slug rule (`[^a-z0-9]+ → _`, spec §10)

    func testOptionNameSlugRule() {
        XCTAssertEqual(HotkeyRegistry.optionName(for: "New Time"), "new_time")
        XCTAssertEqual(HotkeyRegistry.optionName(for: "Save As"), "save_as")
        XCTAssertEqual(HotkeyRegistry.optionName(for: "Copy Mod Note"), "copy_mod_note")
        XCTAssertEqual(HotkeyRegistry.optionName(for: "Settings"), "settings")
        XCTAssertEqual(HotkeyRegistry.optionName(for: "start_paste"), "start_paste")
        XCTAssertEqual(HotkeyRegistry.optionName(for: "Toggle Mode"), "toggle_mode")
        XCTAssertEqual(HotkeyRegistry.optionName(for: "video_mark_load_end"), "video_mark_load_end")
        XCTAssertEqual(HotkeyRegistry.optionName(for: "Paste Start Frame (Loads)"), "paste_start_frame_loads")
        XCTAssertEqual(HotkeyRegistry.optionName(for: "  Weird--Name!! "), "weird_name")
    }

    func testOptionNamesAreUnique() {
        let names = HotkeyRegistry.actions.map { HotkeyRegistry.optionName(for: $0.id) }
        XCTAssertEqual(names.count, Set(names).count)
    }

    // MARK: - Defaults (macOS Cmd swap + new video actions)

    func testDefaults() {
        let defaults = HotkeyRegistry.defaults
        XCTAssertEqual(defaults["New Time"], "Cmd+N")
        XCTAssertEqual(defaults["Save As"], "Cmd+Shift+S")
        XCTAssertEqual(defaults["Settings"], "Cmd+,")
        XCTAssertEqual(defaults["Add Loads"], "Cmd+L")
        XCTAssertEqual(defaults["Toggle Mode"], "Cmd+T")
        XCTAssertEqual(defaults["video_frame_back"], ",")
        XCTAssertEqual(defaults["video_frame_forward"], ".")
        XCTAssertEqual(defaults["video_play_pause"], "Space")
        XCTAssertEqual(defaults["video_mark_start"], "[")
        XCTAssertEqual(defaults["video_mark_end"], "]")
        XCTAssertEqual(defaults["video_mark_load_start"], "L")
        XCTAssertEqual(defaults["video_mark_load_end"], "Shift+L")
    }

    func testNoDuplicateDefaults() {
        XCTAssertTrue(HotkeyRegistry.duplicateGroups(in: HotkeyRegistry.defaults).isEmpty)
    }

    // MARK: - KeyCombo parsing

    func testKeyComboParsing() throws {
        let combo = try XCTUnwrap(KeyCombo(string: "Cmd+Shift+S"))
        XCTAssertTrue(combo.command)
        XCTAssertTrue(combo.shift)
        XCTAssertFalse(combo.option)
        XCTAssertFalse(combo.control)
        XCTAssertEqual(combo.key, "s")
        XCTAssertEqual(combo.canonical, "Cmd+Shift+S")
    }

    func testKeyComboPlainKeys() throws {
        XCTAssertEqual(try XCTUnwrap(KeyCombo(string: ",")).canonical, ",")
        XCTAssertEqual(try XCTUnwrap(KeyCombo(string: "Shift+L")).canonical, "Shift+L")
        XCTAssertEqual(try XCTUnwrap(KeyCombo(string: "Space")).canonical, "Space")
        XCTAssertEqual(try XCTUnwrap(KeyCombo(string: "space")).canonical, "Space")
        XCTAssertEqual(try XCTUnwrap(KeyCombo(string: "ctrl+n")).canonical, "Ctrl+N")
    }

    func testKeyComboInvalid() {
        XCTAssertNil(KeyCombo(string: ""))
        XCTAssertNil(KeyCombo(string: "Cmd+Shift"))
    }

    // MARK: - Duplicate detection

    func testDuplicateDetection() {
        var assignments = HotkeyRegistry.defaults
        assignments["Open Time"] = "Cmd+M" // collides with Copy Mod Note
        let groups = HotkeyRegistry.duplicateGroups(in: assignments)
        XCTAssertEqual(groups.count, 1)
        XCTAssertEqual(Set(groups[0]), Set(["Open Time", "Copy Mod Note"]))
    }

    func testDuplicateDetectionIsCaseInsensitive() {
        var assignments = HotkeyRegistry.defaults
        assignments["Open Time"] = "cmd+m"
        XCTAssertEqual(HotkeyRegistry.duplicateGroups(in: assignments).count, 1)
    }
}
