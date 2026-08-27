import XCTest
@testable import CRTCore

final class TimeSessionTests: XCTestCase {

    // MARK: - Loads mode math (port of time.py)

    func testDefaultsAreZero() {
        let session = TimeSession()
        XCTAssertEqual(session.withLoads, Decimal(0))
        XCTAssertEqual(session.withoutLoads, Decimal(0))
        XCTAssertEqual(session.isoWithLoads(), "00.000")
    }

    func testWithAndWithoutLoads() {
        var session = TimeSession(startFrame: 0, endFrame: 5400, framerate: Decimal(60))
        session.loads = [Load(startFrame: 600, endFrame: 900)]
        XCTAssertEqual(session.lengthWithLoads, 5400)
        XCTAssertEqual(session.lengthWithoutLoads, 5100)
        XCTAssertEqual(session.withLoads, Decimal(90))
        XCTAssertEqual(session.withoutLoads, Decimal(85))
        XCTAssertEqual(session.isoWithLoads(), "01:30.000")
        XCTAssertEqual(session.isoWithoutLoads(), "01:25.000")
    }

    func testNtscFramerateIsExact() {
        let session = TimeSession(startFrame: 0, endFrame: 2997,
                                  framerate: Decimal(string: "29.97") ?? Decimal(0))
        XCTAssertEqual(session.withLoads, Decimal(100))
    }

    func testZeroFramerateNeverDivides() {
        let session = TimeSession(startFrame: 0, endFrame: 100, framerate: Decimal(0))
        XCTAssertEqual(session.withLoads, Decimal(0))
        XCTAssertEqual(session.withoutLoads, Decimal(0))
    }

    /// Python quirk: with_loads guards `int(framerate) == 0` (truncating).
    func testSubOneFramerateQuirk() {
        let session = TimeSession(startFrame: 0, endFrame: 100,
                                  framerate: Decimal(string: "0.5") ?? Decimal(0))
        XCTAssertEqual(session.withLoads, Decimal(0))
        XCTAssertEqual(session.withoutLoads, Decimal(200))
    }

    func testAverageLoadLengthTruncates() {
        var session = TimeSession()
        session.loads = [Load(startFrame: 0, endFrame: 10), Load(startFrame: 0, endFrame: 5)]
        XCTAssertEqual(session.averageLoadLength, 7)
    }

    // MARK: - Validation (exact messages)

    func testAddLoadValidation() {
        var session = TimeSession()

        XCTAssertThrowsError(try session.addLoad(startFrame: 0, endFrame: 0)) { error in
            XCTAssertEqual((error as? CRTError)?.messageText, "You must provide an input for the loads")
        }
        XCTAssertThrowsError(try session.addLoad(startFrame: 5, endFrame: 5)) { error in
            XCTAssertEqual((error as? CRTError)?.messageText, "The duration of the load is 0.000")
        }
        XCTAssertThrowsError(try session.addLoad(startFrame: 10, endFrame: 5)) { error in
            XCTAssertEqual((error as? CRTError)?.messageText, "The load time ends before it starts.")
        }
        XCTAssertNoThrow(try session.addLoad(startFrame: 5, endFrame: 10))
        XCTAssertEqual(session.loads.count, 1)
    }

    func testMutateLoadValidation() throws {
        var session = TimeSession()
        try session.addLoad(startFrame: 5, endFrame: 10)
        XCTAssertThrowsError(try session.mutateLoad(at: 0, startFrame: 20, endFrame: 10))
        XCTAssertNoThrow(try session.mutateLoad(at: 0, startFrame: 6, endFrame: 12))
        XCTAssertEqual(session.loads[0], Load(startFrame: 6, endFrame: 12))
        // Out-of-range index is a no-op, like the Python guard.
        XCTAssertNoThrow(try session.mutateLoad(at: 9, startFrame: 1, endFrame: 2))
    }

    func testDeleteAndClear() throws {
        var session = TimeSession()
        try session.addLoad(startFrame: 5, endFrame: 10)
        try session.addLoad(startFrame: 20, endFrame: 30)
        session.deleteLoad(at: 0)
        XCTAssertEqual(session.loads, [Load(startFrame: 20, endFrame: 30)])
        session.clearLoads()
        XCTAssertTrue(session.loads.isEmpty)
    }

    // MARK: - Segment mode (spec §1.1)

    func testSegmentTotals() {
        var session = TimeSession(framerate: Decimal(50))
        session.mode = .segments
        session.segments = [
            Segment(startFrame: 100, endFrame: 200),
            Segment(startFrame: 300, endFrame: 450),
        ]
        XCTAssertEqual(session.segmentTotalFrames, 250)
        XCTAssertEqual(session.fullRunFrames, 350)
        XCTAssertEqual(session.segmentTotal, Decimal(5))
        XCTAssertEqual(session.fullRun, Decimal(7))
        XCTAssertEqual(session.displayWithoutLoads, Decimal(5))
        XCTAssertEqual(session.displayWithLoads, Decimal(7))
        XCTAssertEqual(session.effectiveStartFrame, 100)
        XCTAssertEqual(session.effectiveEndFrame, 450)
    }

    func testSegmentValidationMatchesLoads() {
        var session = TimeSession(mode: .segments)
        XCTAssertThrowsError(try session.addSegment(startFrame: 0, endFrame: 0))
        XCTAssertThrowsError(try session.addSegment(startFrame: 7, endFrame: 7))
        XCTAssertThrowsError(try session.addSegment(startFrame: 9, endFrame: 3))
        XCTAssertNoThrow(try session.addSegment(startFrame: 3, endFrame: 9))
    }

    // MARK: - Segments ↔ loads gap conversion (spec §3)

    func testGapsBetweenSegments() {
        var session = TimeSession(mode: .segments)
        session.segments = [
            Segment(startFrame: 300, endFrame: 450),
            Segment(startFrame: 100, endFrame: 200),
        ]
        XCTAssertEqual(session.gapsBetweenSegments(), [Load(startFrame: 200, endFrame: 300)])
    }

    func testGapsIdentityBoundsMinusGapsEqualsTotal() {
        var session = TimeSession(mode: .segments)
        session.segments = [
            Segment(startFrame: 0, endFrame: 100),
            Segment(startFrame: 150, endFrame: 300),
            Segment(startFrame: 320, endFrame: 400),
        ]
        let gaps = session.gapsBetweenSegments()
        let gapSum = gaps.reduce(0) { $0 + $1.length }
        XCTAssertEqual(session.fullRunFrames - gapSum, session.segmentTotalFrames)
    }

    func testGapsWithOverlappingSegments() {
        var session = TimeSession(mode: .segments)
        session.segments = [
            Segment(startFrame: 0, endFrame: 100),
            Segment(startFrame: 50, endFrame: 150),
            Segment(startFrame: 200, endFrame: 250),
        ]
        XCTAssertEqual(session.gapsBetweenSegments(), [Load(startFrame: 150, endFrame: 200)])
    }

    func testNoSegmentsNoGaps() {
        let session = TimeSession(mode: .segments)
        XCTAssertTrue(session.gapsBetweenSegments().isEmpty)
        XCTAssertEqual(session.segmentBounds.start, 0)
        XCTAssertEqual(session.segmentBounds.end, 0)
    }
}
