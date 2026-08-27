import Foundation

/// Run-file (*.json) reading and writing (spec §3). The on-disk format stays
/// interchangeable with the Python app: `framerate` is written as a STRING
/// and `loads` as `[[start, end], ...]`. The native apps write a superset
/// (`mode`, `segments`, `meta`) that the Python app ignores.
public enum RunFileStore {

    // MARK: - Decoding

    public static func decode(_ data: Data) throws -> TimeSession {
        guard
            let object = try? JSONSerialization.jsonObject(with: data),
            let dict = object as? [String: Any]
        else {
            throw CRTError.corruptedFile
        }
        return try decodeDictionary(dict)
    }

    public static func decodeDictionary(_ dict: [String: Any]) throws -> TimeSession {
        guard
            let startNumber = dict["start_frame"] as? NSNumber,
            let endNumber = dict["end_frame"] as? NSNumber
        else {
            throw CRTError.corruptedFile
        }

        // framerate: accept string or number.
        let framerate: Decimal
        if let text = dict["framerate"] as? String {
            framerate = Decimal(string: text) ?? Decimal(0)
        } else if let number = dict["framerate"] as? NSNumber {
            framerate = Decimal(string: number.stringValue) ?? Decimal(0)
        } else {
            throw CRTError.corruptedFile
        }

        guard let loadsRaw = dict["loads"] as? [[Any]] else {
            throw CRTError.corruptedFile
        }
        let loads = try framePairs(loadsRaw)

        var session = TimeSession(
            startFrame: startNumber.intValue,
            endFrame: endNumber.intValue,
            framerate: framerate,
            loads: loads
        )

        // Native superset — missing → loads mode.
        if let modeText = dict["mode"] as? String, modeText == TimingMode.segments.rawValue {
            session.mode = .segments
        }
        if let segmentsRaw = dict["segments"] as? [[Any]] {
            session.segments = try framePairs(segmentsRaw)
        }
        if let metaDict = dict["meta"] as? [String: Any] {
            session.meta = SessionMeta(
                title: metaDict["title"] as? String,
                game: metaDict["game"] as? String,
                notes: metaDict["notes"] as? String,
                created: metaDict["created"] as? String,
                modified: metaDict["modified"] as? String,
                videoURL: metaDict["video_url"] as? String
            )
        }
        return session
    }

    /// A malformed pair means the file is corrupt (spec §3) — never drop it
    /// silently, or the next save would overwrite the file with partial data.
    private static func framePairs(_ raw: [[Any]]) throws -> [Load] {
        var result: [Load] = []
        for pair in raw {
            guard pair.count >= 2,
                  let start = pair[0] as? NSNumber,
                  let end = pair[1] as? NSNumber else {
                throw CRTError.corruptedFile
            }
            result.append(Load(startFrame: start.intValue, endFrame: end.intValue))
        }
        return result
    }

    // MARK: - Encoding

    public static func encodeDictionary(_ session: TimeSession) -> [String: Any] {
        var dict: [String: Any] = [:]

        if session.mode == .segments {
            // Degrade gracefully in the Python app: run bounds are the segment
            // bounds and the gaps between segments are written as loads.
            let bounds = session.segmentBounds
            dict["start_frame"] = bounds.start
            dict["end_frame"] = bounds.end
            dict["loads"] = session.gapsBetweenSegments().map { [$0.startFrame, $0.endFrame] }
        } else {
            dict["start_frame"] = session.startFrame
            dict["end_frame"] = session.endFrame
            dict["loads"] = session.loads.map { [$0.startFrame, $0.endFrame] }
        }

        dict["framerate"] = TimeFormatter.string(session.framerate)
        dict["mode"] = session.mode.rawValue
        dict["segments"] = session.segments.map { [$0.startFrame, $0.endFrame] }

        if !session.meta.isEmpty {
            var meta: [String: Any] = [:]
            if let title = session.meta.title { meta["title"] = title }
            if let game = session.meta.game { meta["game"] = game }
            if let notes = session.meta.notes { meta["notes"] = notes }
            if let created = session.meta.created { meta["created"] = created }
            if let modified = session.meta.modified { meta["modified"] = modified }
            if let videoURL = session.meta.videoURL { meta["video_url"] = videoURL }
            dict["meta"] = meta
        }

        return dict
    }

    public static func encode(_ session: TimeSession) throws -> Data {
        let dict = encodeDictionary(session)
        return try JSONSerialization.data(withJSONObject: dict, options: [.sortedKeys])
    }

    // MARK: - Disk

    public static func load(from url: URL) throws -> TimeSession {
        let data: Data
        do {
            data = try Data(contentsOf: url)
        } catch {
            throw CRTError.message("The file could not be opened: \(url.path)")
        }
        return try decode(data)
    }

    public static func save(_ session: TimeSession, to url: URL) throws {
        let data = try encode(session)
        try data.write(to: url, options: [.atomic])
    }
}

/// Owns the current session, its on-disk path, per-session history and the
/// dirty flag. Port of `src/crt/file_manager.py`.
public struct SessionFileManager: Sendable {
    public var session: TimeSession
    public var filePath: String?
    public var pastFilePaths: [String]
    public var dirty: Bool

    public init(session: TimeSession = TimeSession()) {
        self.session = session
        self.filePath = nil
        self.pastFilePaths = []
        self.dirty = false
    }

    /// Past file paths, excluding whichever file is currently active.
    public func history() -> [String] {
        pastFilePaths.filter { $0 != filePath }
    }

    private mutating func rememberPastPath(_ path: String?) {
        guard let path, path != filePath, !pastFilePaths.contains(path) else { return }
        pastFilePaths.append(path)
    }

    public mutating func newSession(mode: TimingMode = .loads) {
        let oldPath = filePath
        filePath = nil
        session = TimeSession(mode: mode)
        dirty = false
        rememberPastPath(oldPath)
    }

    public mutating func load(from path: String) throws {
        let loaded = try RunFileStore.load(from: URL(fileURLWithPath: path))
        let oldPath = filePath
        session = loaded
        filePath = path
        dirty = false
        pastFilePaths.removeAll { $0 == path }
        rememberPastPath(oldPath)
    }

    private mutating func stampMeta() {
        let now = ISO8601DateFormatter().string(from: Date())
        if session.meta.created == nil {
            session.meta.created = now
        }
        session.meta.modified = now
    }

    public mutating func save() throws {
        guard let path = filePath else {
            throw CRTError.noFilePath
        }
        stampMeta()
        try RunFileStore.save(session, to: URL(fileURLWithPath: path))
        dirty = false
    }

    public mutating func saveAs(path: String) throws {
        let oldPath = filePath
        filePath = path
        stampMeta()
        try RunFileStore.save(session, to: URL(fileURLWithPath: path))
        dirty = false
        rememberPastPath(oldPath)
    }
}
