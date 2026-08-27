import XCTest
@testable import CRTCore

final class FrameInputParserTests: XCTestCase {

    private let fps60 = Decimal(60)

    // MARK: - Plain input (spec §2 steps 2–5)

    func testPlainInteger() throws {
        XCTAssertEqual(try FrameInputParser.parse("123", framerate: fps60), 123)
    }

    func testStripsGarbage() throws {
        XCTAssertEqual(try FrameInputParser.parse("1a2b3", framerate: fps60), 123)
        XCTAssertEqual(try FrameInputParser.parse("  456 frames ", framerate: fps60), 456)
    }

    func testEmptyAndNoDigits() throws {
        XCTAssertEqual(try FrameInputParser.parse("", framerate: fps60), 0)
        XCTAssertEqual(try FrameInputParser.parse("abc", framerate: fps60), 0)
        XCTAssertEqual(try FrameInputParser.parse("...", framerate: fps60), 0)
    }

    func testDecimalTreatedAsSeconds() throws {
        XCTAssertEqual(try FrameInputParser.parse("1.5", framerate: fps60), 90)
        // Timestamp-ish input: "00:01.5" strips to "001.5" seconds.
        XCTAssertEqual(try FrameInputParser.parse("00:01.5", framerate: fps60), 90)
    }

    func testMultipleDotsCollapse() throws {
        // "1.2.5" → "1.25" seconds → 75 frames at 60.
        XCTAssertEqual(try FrameInputParser.parse("1.2.5", framerate: fps60), 75)
    }

    func testDecimalWithZeroFramerate() throws {
        XCTAssertEqual(try FrameInputParser.parse("1.5", framerate: Decimal(0)), 0)
    }

    // MARK: - Debug info (spec §2 step 1)

    func testDebugInfoStringCmt() throws {
        let text = "some prefix {\"cmt\": \"12.5\", \"docid\": \"abc\", \"fmt\": \"243\"}"
        XCTAssertEqual(try FrameInputParser.parse(text, framerate: fps60), 750)
    }

    func testDebugInfoNumericCmt() throws {
        let text = "{\"cmt\": 2.0, \"docid\": \"abc\", \"fmt\": 243}"
        XCTAssertEqual(try FrameInputParser.parse(text, framerate: fps60), 120)
    }

    func testInvalidDebugInfoThrowsExactMessage() {
        let text = "{\"cmt\": }"
        XCTAssertThrowsError(try FrameInputParser.parse(text, framerate: fps60)) { error in
            XCTAssertEqual(
                (error as? CRTError)?.messageText,
                "The debug info provided is invalid.\nPlease re-enter debug info."
            )
        }
    }

    func testMissingCmtIsNotDebugInfo() throws {
        // No "cmt" key text → not debug info; digits get stripped out.
        XCTAssertEqual(try FrameInputParser.parse("{\"foo\": 1}", framerate: fps60), 1)
    }

    func testExtractDebugInfoIDs() {
        let text = "junk {\"cmt\": \"1\", \"docid\": \"dQw4w9WgXcQ\", \"fmt\": 244}"
        let ids = FrameInputParser.extractDebugInfoIDs(text)
        XCTAssertEqual(ids?.videoID, "dQw4w9WgXcQ")
        XCTAssertEqual(ids?.formatID, "244")

        XCTAssertNil(FrameInputParser.extractDebugInfoIDs("{\"cmt\": \"1\"}"))
        XCTAssertNil(FrameInputParser.extractDebugInfoIDs("no json"))
        XCTAssertNil(FrameInputParser.extractDebugInfoIDs("{\"docid\": \"\", \"fmt\": \"1\"}"))
    }

    func testIsDebugInfo() {
        XCTAssertTrue(FrameInputParser.isDebugInfo("{\"cmt\": \"1\"}"))
        XCTAssertFalse(FrameInputParser.isDebugInfo("cmt but no braces"))
        XCTAssertFalse(FrameInputParser.isDebugInfo("{\"other\": 1}"))
    }

    // MARK: - cleanFramerate

    func testCleanFramerate() {
        XCTAssertEqual(FrameInputParser.cleanFramerate("60fps"), Decimal(60))
        XCTAssertEqual(FrameInputParser.cleanFramerate(""), Decimal(0))
        XCTAssertEqual(FrameInputParser.cleanFramerate("."), Decimal(0))
        XCTAssertEqual(FrameInputParser.cleanFramerate("59.94"), Decimal(string: "59.94"))
        XCTAssertEqual(FrameInputParser.cleanFramerate("60."), Decimal(60))
        XCTAssertEqual(FrameInputParser.cleanFramerate("6..0"), Decimal(6))
        XCTAssertEqual(FrameInputParser.cleanFramerate("2 9.97"), Decimal(string: "29.97"))
    }
}
