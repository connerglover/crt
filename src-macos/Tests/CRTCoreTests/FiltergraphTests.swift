import XCTest
@testable import CRTCore

final class FiltergraphTests: XCTestCase {

    private func dec(_ text: String) -> Decimal {
        Decimal(string: text) ?? Decimal(0)
    }

    /// The shared style/position prefix for a 432-px-high video (fontsize
    /// 24), pill style, bottom-right corner.
    private let style = "fontfile='/System/Library/Fonts/Menlo.ttc':fontsize=24:fontcolor=white:"
        + "box=1:boxcolor=black@0.55:boxborderw=10:x=w-tw-24:y=h-th-24"

    /// The reference scenario verified against real ffmpeg output:
    /// run start t=1, one load 2→3, run end 4.5, no trim offset.
    func testReferenceScenarioExactStrings() {
        let spec = TimerOverlaySpec(videoHeight: 432, corner: .bottomRight, style: .pill)
        let chain = TimerFiltergraphBuilder.build(
            runStart: Decimal(1),
            runEnd: dec("4.5"),
            pauses: [(start: Decimal(2), end: Decimal(3))],
            trimStart: Decimal(0),
            spec: spec
        )

        let running1 = "%{eif\\:trunc((t-1)/3600)\\:d\\:2}\\:"
            + "%{eif\\:trunc(mod((t-1)/60,60))\\:d\\:2}\\:"
            + "%{eif\\:trunc(mod(t-1,60))\\:d\\:2}."
            + "%{eif\\:trunc(mod((t-1)*1000,1000))\\:d\\:3}"
        let running2 = "%{eif\\:trunc((t-2)/3600)\\:d\\:2}\\:"
            + "%{eif\\:trunc(mod((t-2)/60,60))\\:d\\:2}\\:"
            + "%{eif\\:trunc(mod(t-2,60))\\:d\\:2}."
            + "%{eif\\:trunc(mod((t-2)*1000,1000))\\:d\\:3}"

        let expected = [
            "drawtext=\(style):enable='lt(t,1)':text='00\\:00\\:00.000'",
            "drawtext=\(style):enable='between(t,1,2)':text='\(running1)'",
            "drawtext=\(style):enable='between(t,2,3)':text='00\\:00\\:01.000'",
            "drawtext=\(style):enable='between(t,3,4.5)':text='\(running2)'",
            "drawtext=\(style):enable='gt(t,4.5)':text='00\\:00\\:02.500'",
        ].joined(separator: ",")

        XCTAssertEqual(chain, expected)
    }

    /// Same scenario shifted by a trim start: every window time is relative
    /// to the trimmed video.
    func testTrimStartShiftsWindows() {
        let spec = TimerOverlaySpec(videoHeight: 432, corner: .bottomRight, style: .pill)
        let chain = TimerFiltergraphBuilder.build(
            runStart: Decimal(1),
            runEnd: dec("4.5"),
            pauses: [(start: Decimal(2), end: Decimal(3))],
            trimStart: dec("0.5"),
            spec: spec
        )
        XCTAssertTrue(chain.contains("enable='lt(t,0.5)'"))
        XCTAssertTrue(chain.contains("enable='between(t,0.5,1.5)'"))
        XCTAssertTrue(chain.contains("enable='between(t,1.5,2.5)'"))
        XCTAssertTrue(chain.contains("enable='between(t,2.5,4)'"))
        XCTAssertTrue(chain.contains("enable='gt(t,4)'"))
        // Offsets shift with the trim, elapsed times do not.
        XCTAssertTrue(chain.contains("trunc((t-0.5)/3600)"))
        XCTAssertTrue(chain.contains("trunc((t-1.5)/3600)"))
        XCTAssertTrue(chain.contains("text='00\\:00\\:02.500'"))
    }

    func testNoPausesSingleRunningWindow() {
        let spec = TimerOverlaySpec(videoHeight: 1080, corner: .topLeft, style: .plain)
        let chain = TimerFiltergraphBuilder.build(
            runStart: Decimal(0),
            runEnd: Decimal(10),
            pauses: [],
            trimStart: Decimal(0),
            spec: spec
        )
        // No pre-run window when the run starts at 0; plain style has no box.
        XCTAssertFalse(chain.contains("lt(t,"))
        XCTAssertFalse(chain.contains("box=1"))
        XCTAssertTrue(chain.contains("fontsize=60"))
        XCTAssertTrue(chain.contains("x=24:y=24"))
        XCTAssertTrue(chain.contains("enable='between(t,0,10)'"))
        XCTAssertTrue(chain.hasSuffix("enable='gt(t,10)':text='00\\:00\\:10.000'"))
    }

    func testCornerPositions() {
        XCTAssertEqual(TimerCorner.topLeft.positionOptions, "x=24:y=24")
        XCTAssertEqual(TimerCorner.topRight.positionOptions, "x=w-tw-24:y=24")
        XCTAssertEqual(TimerCorner.bottomLeft.positionOptions, "x=24:y=h-th-24")
        XCTAssertEqual(TimerCorner.bottomRight.positionOptions, "x=w-tw-24:y=h-th-24")
    }

    func testConstantClockFormatting() {
        XCTAssertEqual(TimerFiltergraphBuilder.constantClock(Decimal(0)), "00\\:00\\:00.000")
        XCTAssertEqual(TimerFiltergraphBuilder.constantClock(dec("2.5")), "00\\:00\\:02.500")
        XCTAssertEqual(TimerFiltergraphBuilder.constantClock(dec("3661.007")), "01\\:01\\:01.007")
        XCTAssertEqual(TimerFiltergraphBuilder.constantClock(Decimal(-1)), "00\\:00\\:00.000")
    }

    func testTrimBounds() {
        // Lead/tail of 2 s, clamped to the video.
        let bounds = TimerFiltergraphBuilder.trimBounds(
            runStart: Decimal(1), runEnd: dec("4.5"), videoDuration: Decimal(5))
        XCTAssertEqual(bounds.start, Decimal(0))
        XCTAssertEqual(bounds.end, Decimal(5))

        let bounds2 = TimerFiltergraphBuilder.trimBounds(
            runStart: Decimal(10), runEnd: Decimal(20), videoDuration: Decimal(100))
        XCTAssertEqual(bounds2.start, Decimal(8))
        XCTAssertEqual(bounds2.end, Decimal(22))
    }

    // MARK: - ffmpeg command shape (spec §9.3)

    func testExporterArguments() {
        let job = ExportJob(
            input: URL(fileURLWithPath: "/tmp/in.mp4"),
            output: URL(fileURLWithPath: "/tmp/out.mp4"),
            trimStart: Decimal(0),
            trimEnd: Decimal(5),
            filtergraph: "CHAIN"
        )
        XCTAssertEqual(FfmpegExporter.arguments(for: job), [
            "-y",
            "-ss", "0",
            "-to", "5",
            "-i", "/tmp/in.mp4",
            "-vf", "CHAIN",
            "-c:v", "libx264",
            "-preset", "veryfast",
            "-crf", "18",
            "-c:a", "aac",
            "-movflags", "+faststart",
            "/tmp/out.mp4",
        ])
    }

    func testStderrTimeParsing() {
        let line = "frame=  100 fps= 30 q=28.0 size=     256kB time=00:01:23.45 bitrate=1000kbits/s speed=1x"
        XCTAssertEqual(FfmpegExporter.parseTimeSeconds(fromStderrLine: line), 83.45)
        XCTAssertNil(FfmpegExporter.parseTimeSeconds(fromStderrLine: "no time here"))
        XCTAssertNil(FfmpegExporter.parseTimeSeconds(fromStderrLine: "time=N/A bitrate=N/A"))
    }
}
