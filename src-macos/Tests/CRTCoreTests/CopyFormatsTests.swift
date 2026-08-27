import XCTest
@testable import CRTCore

final class CopyFormatsTests: XCTestCase {

    // MARK: - Discord message (spec §5)

    func testDiscordMessageWithLoads() {
        var session = TimeSession(startFrame: 0, endFrame: 5400, framerate: Decimal(60))
        session.loads = [Load(startFrame: 600, endFrame: 690)]
        let expected = """
        ```
        Time: 01:28.500
        Time (with loads): 01:30.000

        Loads (1):
        1. 10.000 - 11.500 (01.500)
        ```
        """
        XCTAssertEqual(CopyFormats.discordMessage(session: session), expected)
    }

    func testDiscordMessageWithoutLoads() {
        let session = TimeSession(startFrame: 0, endFrame: 1800, framerate: Decimal(60))
        let expected = """
        ```
        Time: 30.000
        Time (with loads): 30.000
        ```
        """
        XCTAssertEqual(CopyFormats.discordMessage(session: session), expected)
    }

    func testDiscordMessageSegmentMode() {
        var session = TimeSession(framerate: Decimal(60))
        session.mode = .segments
        session.segments = [
            Segment(startFrame: 300, endFrame: 600),
            Segment(startFrame: 900, endFrame: 1500),
        ]
        let expected = """
        ```
        Time: 15.000
        Time (with loads): 20.000

        Segments (2):
        1. 05.000 - 10.000 (05.000)
        2. 15.000 - 25.000 (10.000)
        ```
        """
        XCTAssertEqual(CopyFormats.discordMessage(session: session), expected)
    }

    // MARK: - YouTube chapters (spec §5)

    func testYouTubeChaptersSortsLoads() {
        var session = TimeSession(startFrame: 0, endFrame: 5400, framerate: Decimal(60))
        session.loads = [
            Load(startFrame: 600, endFrame: 690),
            Load(startFrame: 60, endFrame: 120),
        ]
        let expected = """
        0:00 Gameplay
        0:01 Loading
        0:02 Gameplay
        0:10 Loading
        0:11 Gameplay
        """
        XCTAssertEqual(CopyFormats.youtubeChapters(session: session), expected)
    }

    func testYouTubeChaptersNoLoads() {
        let session = TimeSession(startFrame: 0, endFrame: 5400, framerate: Decimal(60))
        XCTAssertEqual(CopyFormats.youtubeChapters(session: session), "0:00 Gameplay")
    }

    func testYouTubeChaptersSegmentMode() {
        var session = TimeSession(framerate: Decimal(60))
        session.mode = .segments
        session.segments = [
            Segment(startFrame: 900, endFrame: 1500),
            Segment(startFrame: 300, endFrame: 600),
        ]
        let expected = """
        0:00 Waiting
        0:05 Segment 1
        0:10 Waiting
        0:15 Segment 2
        0:25 Waiting
        """
        XCTAssertEqual(CopyFormats.youtubeChapters(session: session), expected)
    }
}
