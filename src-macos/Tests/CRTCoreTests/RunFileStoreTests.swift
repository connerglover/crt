import XCTest
@testable import CRTCore

final class RunFileStoreTests: XCTestCase {

    private func tempDir() throws -> URL {
        let url = FileManager.default.temporaryDirectory
            .appendingPathComponent("crt-tests-\(UUID().uuidString)", isDirectory: true)
        try FileManager.default.createDirectory(at: url, withIntermediateDirectories: true)
        return url
    }

    // MARK: - Reading the exact Python format (spec §3)

    func testDecodePythonFormat() throws {
        let json = "{\"start_frame\": 100, \"end_frame\": 5000, \"framerate\": \"59.94\", \"loads\": [[200, 300]]}"
        let session = try RunFileStore.decode(Data(json.utf8))
        XCTAssertEqual(session.startFrame, 100)
        XCTAssertEqual(session.endFrame, 5000)
        XCTAssertEqual(session.framerate, Decimal(string: "59.94"))
        XCTAssertEqual(session.loads, [Load(startFrame: 200, endFrame: 300)])
        XCTAssertEqual(session.mode, .loads)
        XCTAssertTrue(session.segments.isEmpty)
    }

    func testDecodeAcceptsNumericFramerate() throws {
        let json = "{\"start_frame\": 0, \"end_frame\": 0, \"framerate\": 60, \"loads\": []}"
        let session = try RunFileStore.decode(Data(json.utf8))
        XCTAssertEqual(session.framerate, Decimal(60))
    }

    func testCorruptFileMessage() {
        XCTAssertThrowsError(try RunFileStore.decode(Data("not json".utf8))) { error in
            XCTAssertEqual((error as? CRTError)?.messageText, "The file provided is corrupted.")
        }
        XCTAssertThrowsError(try RunFileStore.decode(Data("{\"start_frame\": 1}".utf8))) { error in
            XCTAssertEqual((error as? CRTError)?.messageText, "The file provided is corrupted.")
        }
    }

    // MARK: - Writing stays Python-compatible

    func testEncodeWritesFramerateAsString() throws {
        let session = TimeSession(startFrame: 0, endFrame: 5000,
                                  framerate: Decimal(string: "29.97") ?? Decimal(0),
                                  loads: [Load(startFrame: 100, endFrame: 200)])
        let data = try RunFileStore.encode(session)
        let object = try JSONSerialization.jsonObject(with: data)
        let dict = try XCTUnwrap(object as? [String: Any])

        XCTAssertEqual(dict["framerate"] as? String, "29.97")
        XCTAssertEqual(dict["start_frame"] as? Int, 0)
        XCTAssertEqual(dict["end_frame"] as? Int, 5000)
        let loads = try XCTUnwrap(dict["loads"] as? [[Int]])
        XCTAssertEqual(loads, [[100, 200]])
        XCTAssertEqual(dict["mode"] as? String, "loads")
    }

    /// Segment mode degrades gracefully for the Python app: run bounds and
    /// the inter-segment gaps as loads (spec §3).
    func testEncodeSegmentModeWritesGapsAsLoads() throws {
        var session = TimeSession(framerate: Decimal(60))
        session.mode = .segments
        session.segments = [
            Segment(startFrame: 100, endFrame: 200),
            Segment(startFrame: 300, endFrame: 450),
        ]
        let data = try RunFileStore.encode(session)
        let dict = try XCTUnwrap(try JSONSerialization.jsonObject(with: data) as? [String: Any])

        XCTAssertEqual(dict["start_frame"] as? Int, 100)
        XCTAssertEqual(dict["end_frame"] as? Int, 450)
        XCTAssertEqual(dict["mode"] as? String, "segments")
        XCTAssertEqual(try XCTUnwrap(dict["loads"] as? [[Int]]), [[200, 300]])
        XCTAssertEqual(try XCTUnwrap(dict["segments"] as? [[Int]]), [[100, 200], [300, 450]])
    }

    func testRoundTripLoadsMode() throws {
        var session = TimeSession(startFrame: 5, endFrame: 100, framerate: Decimal(string: "59.94") ?? Decimal(0))
        session.loads = [Load(startFrame: 10, endFrame: 20), Load(startFrame: 40, endFrame: 60)]
        session.meta.title = "Any%"
        session.meta.game = "Some Game"
        session.meta.videoURL = "https://youtu.be/abc"

        let decoded = try RunFileStore.decode(try RunFileStore.encode(session))
        XCTAssertEqual(decoded.startFrame, session.startFrame)
        XCTAssertEqual(decoded.endFrame, session.endFrame)
        XCTAssertEqual(decoded.framerate, session.framerate)
        XCTAssertEqual(decoded.loads, session.loads)
        XCTAssertEqual(decoded.mode, .loads)
        XCTAssertEqual(decoded.meta.title, "Any%")
        XCTAssertEqual(decoded.meta.game, "Some Game")
        XCTAssertEqual(decoded.meta.videoURL, "https://youtu.be/abc")
    }

    func testRoundTripSegmentMode() throws {
        var session = TimeSession(framerate: Decimal(30))
        session.mode = .segments
        session.segments = [
            Segment(startFrame: 100, endFrame: 200),
            Segment(startFrame: 300, endFrame: 450),
        ]
        let decoded = try RunFileStore.decode(try RunFileStore.encode(session))
        XCTAssertEqual(decoded.mode, .segments)
        XCTAssertEqual(decoded.segments, session.segments)
        XCTAssertEqual(decoded.startFrame, 100)
        XCTAssertEqual(decoded.endFrame, 450)
        // Full run minus gap-loads equals the segment total in the Python app.
        XCTAssertEqual(decoded.lengthWithoutLoads, session.segmentTotalFrames)
    }

    // MARK: - SessionFileManager (port of file_manager.py)

    func testFileManagerHistoryRules() throws {
        let dir = try tempDir()
        defer { try? FileManager.default.removeItem(at: dir) }
        let path1 = dir.appendingPathComponent("one.json").path
        let path2 = dir.appendingPathComponent("two.json").path

        var manager = SessionFileManager()
        manager.session.endFrame = 600
        try manager.saveAs(path: path1)
        XCTAssertEqual(manager.filePath, path1)
        XCTAssertFalse(manager.dirty)
        XCTAssertTrue(manager.history().isEmpty)

        try manager.saveAs(path: path2)
        XCTAssertEqual(manager.history(), [path1])

        try manager.load(from: path1)
        XCTAssertEqual(manager.history(), [path2])
        XCTAssertEqual(manager.session.endFrame, 600)

        manager.newSession()
        XCTAssertNil(manager.filePath)
        XCTAssertTrue(manager.history().contains(path1))
        XCTAssertEqual(manager.session.endFrame, 0)
    }

    func testSaveWithoutPathThrows() {
        var manager = SessionFileManager()
        XCTAssertThrowsError(try manager.save())
    }

    // MARK: - Autosave round trip (spec §14)

    func testAutosaveRoundTrip() throws {
        let dir = try tempDir()
        defer { try? FileManager.default.removeItem(at: dir) }
        let autosave = AutosaveService(configDir: dir)

        var session = TimeSession(startFrame: 10, endFrame: 500, framerate: Decimal(60))
        session.loads = [Load(startFrame: 50, endFrame: 80)]
        autosave.save(session: session, filePath: "/tmp/somewhere.json")
        XCTAssertTrue(autosave.exists)

        let restored = try XCTUnwrap(autosave.restore())
        XCTAssertEqual(restored.session.startFrame, 10)
        XCTAssertEqual(restored.session.endFrame, 500)
        XCTAssertEqual(restored.session.loads, session.loads)
        XCTAssertEqual(restored.filePath, "/tmp/somewhere.json")

        autosave.clear()
        XCTAssertFalse(autosave.exists)
        XCTAssertNil(autosave.restore())
    }

    // MARK: - Recents (spec §6)

    func testRecentFilesCapAndOrder() throws {
        let dir = try tempDir()
        defer { try? FileManager.default.removeItem(at: dir) }
        let store = RecentFilesStore(configDir: dir)

        for index in 0..<25 {
            store.add("/tmp/file-\(index).json")
        }
        var paths = store.load()
        XCTAssertEqual(paths.count, 20)
        XCTAssertEqual(paths.first, "/tmp/file-24.json")

        // Re-adding moves to the front without duplicating.
        store.add("/tmp/file-10.json")
        paths = store.load()
        XCTAssertEqual(paths.first, "/tmp/file-10.json")
        XCTAssertEqual(paths.filter { $0 == "/tmp/file-10.json" }.count, 1)
    }
}
