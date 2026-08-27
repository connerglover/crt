import XCTest
@testable import CRTCore

final class TimeFormatterTests: XCTestCase {

    private func dec(_ text: String) -> Decimal {
        guard let value = Decimal(string: text) else {
            XCTFail("bad decimal literal \(text)")
            return Decimal(0)
        }
        return value
    }

    // MARK: - ISO format (spec §5)

    func testIsoZero() {
        XCTAssertEqual(TimeFormatter.iso(Decimal(0)), "00.000")
    }

    func testIsoSubMinute() {
        XCTAssertEqual(TimeFormatter.iso(dec("5.5")), "05.500")
        XCTAssertEqual(TimeFormatter.iso(dec("59.999")), "59.999")
        XCTAssertEqual(TimeFormatter.iso(dec("0.05")), "00.050")
    }

    func testIsoExactMinute() {
        XCTAssertEqual(TimeFormatter.iso(Decimal(60)), "01:00.000")
    }

    func testIsoMinutesAreZeroPadded() {
        XCTAssertEqual(TimeFormatter.iso(dec("65.05")), "01:05.050")
        XCTAssertEqual(TimeFormatter.iso(dec("754.123")), "12:34.123")
    }

    func testIsoHours() {
        XCTAssertEqual(TimeFormatter.iso(Decimal(3600)), "01:00:00.000")
        XCTAssertEqual(TimeFormatter.iso(dec("3661.007")), "01:01:01.007")
        XCTAssertEqual(TimeFormatter.iso(dec("36000.5")), "10:00:00.500")
    }

    func testIsoNegativeClampsToZero() {
        XCTAssertEqual(TimeFormatter.iso(Decimal(-5)), "00.000")
        XCTAssertEqual(TimeFormatter.iso(dec("-0.001")), "00.000")
    }

    // MARK: - Components (port of format_components)

    func testComponentsPadding() {
        let (h, m, s, ms) = TimeFormatter.components(Decimal(3600))
        XCTAssertEqual(h, "01")
        XCTAssertEqual(m, "00")
        XCTAssertEqual(s, "00")
        XCTAssertEqual(ms, "000")
    }

    func testComponentsFractionIsPositional() {
        // 0.05 s is 50 ms → "050", never "005".
        let (_, _, _, ms) = TimeFormatter.components(dec("0.05"))
        XCTAssertEqual(ms, "050")
        let (_, _, _, ms2) = TimeFormatter.components(dec("0.5"))
        XCTAssertEqual(ms2, "500")
    }

    func testComponentsLarger() {
        let (h, m, s, ms) = TimeFormatter.components(dec("7325.5"))
        XCTAssertEqual([h, m, s, ms], ["02", "02", "05", "500"])
    }

    // MARK: - Rounding

    func testRoundedHalfAwayFromZero() {
        XCTAssertEqual(TimeFormatter.rounded(dec("1.2345"), scale: 3), dec("1.235"))
        XCTAssertEqual(TimeFormatter.roundedToInt(dec("2.5")), 3)
        XCTAssertEqual(TimeFormatter.roundedToInt(dec("2.4")), 2)
    }

    func testTruncatedToInt() {
        XCTAssertEqual(TimeFormatter.truncatedToInt(dec("0.9")), 0)
        XCTAssertEqual(TimeFormatter.truncatedToInt(dec("29.97")), 29)
        XCTAssertEqual(TimeFormatter.truncatedToInt(dec("-1.5")), -1)
    }

    func testDecimalString() {
        XCTAssertEqual(TimeFormatter.string(dec("29.97")), "29.97")
        XCTAssertEqual(TimeFormatter.string(Decimal(60)), "60")
    }

    // MARK: - Frame time

    func testFrameTime() {
        XCTAssertEqual(TimeFormatter.frameTime(frames: 90, framerate: Decimal(60), precision: 3), "01.500")
        XCTAssertEqual(TimeFormatter.frameTime(frames: 0, framerate: Decimal(0), precision: 3), "00.000")
        XCTAssertEqual(TimeFormatter.frameTime(frames: 5400, framerate: Decimal(60), precision: 3), "01:30.000")
    }

    // MARK: - YouTube timestamps (floor to seconds)

    func testYouTubeTimestamp() {
        XCTAssertEqual(TimeFormatter.youtubeTimestamp(frame: 0, framerate: Decimal(60)), "0:00")
        XCTAssertEqual(TimeFormatter.youtubeTimestamp(frame: 90, framerate: Decimal(60)), "0:01")
        XCTAssertEqual(TimeFormatter.youtubeTimestamp(frame: 3600, framerate: Decimal(60)), "1:00")
        XCTAssertEqual(TimeFormatter.youtubeTimestamp(frame: 108000, framerate: Decimal(30)), "1:00:00")
        XCTAssertEqual(TimeFormatter.youtubeTimestamp(frame: 100, framerate: Decimal(0)), "0:00")
    }

    func testSrcFormat() {
        XCTAssertEqual(TimeFormatter.srcFormat(dec("3661.5")), "01h 01m 01s 500ms")
    }
}
