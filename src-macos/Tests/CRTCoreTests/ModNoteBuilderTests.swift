import XCTest
@testable import CRTCore

final class ModNoteBuilderTests: XCTestCase {

    private func exampleSession() -> TimeSession {
        // 45 s with loads, 30 s without, at 60 fps.
        var session = TimeSession(startFrame: 0, endFrame: 2700, framerate: Decimal(60))
        session.loads = [Load(startFrame: 900, endFrame: 1800)]
        return session
    }

    func testDefaultTemplate() {
        let note = ModNoteBuilder.build(
            template: "Mod Note: Retimed to {time_without_loads}",
            session: exampleSession()
        )
        XCTAssertEqual(note, "Mod Note: Retimed to 30.000")
    }

    /// The documented example from `Mod Note Format.MD`.
    func testReadmeExample() {
        let note = ModNoteBuilder.build(
            template: "Mod Note {time_without_loads} without loads, and {time_with_loads} with loads at {fps} FPS using {plug}",
            session: exampleSession()
        )
        XCTAssertEqual(
            note,
            "Mod Note 30.000 without loads, and 45.000 with loads at 60 FPS using "
            + "[Conner's Retime Tool](https://github.com/connerglover/conners-retime-tool)"
        )
    }

    func testAllComponentPlaceholders() {
        var session = TimeSession(startFrame: 30, endFrame: 90, framerate: Decimal(60))
        session.loads = []
        let values = ModNoteBuilder.placeholderValues(for: session)
        XCTAssertEqual(values["time_with_loads"], "01.000")
        XCTAssertEqual(values["hours"], "00")
        XCTAssertEqual(values["minutes"], "00")
        XCTAssertEqual(values["seconds"], "01")
        XCTAssertEqual(values["milliseconds"], "000")
        XCTAssertEqual(values["start_frame"], "30")
        XCTAssertEqual(values["end_frame"], "90")
        XCTAssertEqual(values["start_time"], "0.5")
        XCTAssertEqual(values["end_time"], "1.5")
        XCTAssertEqual(values["total_frames"], "60")
        XCTAssertEqual(values["fps"], "60")
    }

    func testZeroFramerateTimes() {
        let session = TimeSession(startFrame: 30, endFrame: 90, framerate: Decimal(0))
        let values = ModNoteBuilder.placeholderValues(for: session)
        XCTAssertEqual(values["start_time"], "0")
        XCTAssertEqual(values["end_time"], "0")
    }

    /// Unknown placeholders must be left literal, never crash (spec §5).
    func testUnknownPlaceholderLeftLiteral() {
        let note = ModNoteBuilder.build(
            template: "x {unknown} y {fps}",
            session: exampleSession()
        )
        XCTAssertEqual(note, "x {unknown} y 60")
    }

    func testMalformedBracesLeftLiteral() {
        let note = ModNoteBuilder.build(template: "open { close } and {fps", session: exampleSession())
        XCTAssertEqual(note, "open { close } and {fps")
    }

    func testEscapedBraces() {
        let note = ModNoteBuilder.build(template: "{{fps}} is {fps}", session: exampleSession())
        XCTAssertEqual(note, "{fps} is 60")
    }

    /// Segment mode: without = segment total, with = full-run span (spec §1.1).
    func testSegmentModeUsesSegmentTimes() {
        var session = TimeSession(framerate: Decimal(60))
        session.mode = .segments
        session.segments = [
            Segment(startFrame: 300, endFrame: 600),
            Segment(startFrame: 900, endFrame: 1500),
        ]
        let values = ModNoteBuilder.placeholderValues(for: session)
        XCTAssertEqual(values["time_without_loads"], "15.000")
        XCTAssertEqual(values["time_with_loads"], "20.000")
        XCTAssertEqual(values["start_frame"], "300")
        XCTAssertEqual(values["end_frame"], "1500")
        XCTAssertEqual(values["total_frames"], "1200")
    }
}
