import Foundation

/// A load (or segment) — a pair of frame positions. Port of `src/crt/load.py`.
public struct Load: Sendable, Equatable, Hashable {
    public var startFrame: Int
    public var endFrame: Int

    public init(startFrame: Int, endFrame: Int) {
        self.startFrame = startFrame
        self.endFrame = endFrame
    }

    public var length: Int { endFrame - startFrame }
}

/// Segments share the exact same shape and validation as loads.
public typealias Segment = Load

/// The retimer's two modes (spec §1.1).
public enum TimingMode: String, Sendable, Equatable, CaseIterable {
    case loads
    case segments
}

/// Optional metadata written into the native run-file superset (spec §3).
public struct SessionMeta: Sendable, Equatable {
    public var title: String?
    public var game: String?
    public var notes: String?
    public var created: String?
    public var modified: String?
    public var videoURL: String?

    public init(title: String? = nil, game: String? = nil, notes: String? = nil,
                created: String? = nil, modified: String? = nil, videoURL: String? = nil) {
        self.title = title
        self.game = game
        self.notes = notes
        self.created = created
        self.modified = modified
        self.videoURL = videoURL
    }

    public var isEmpty: Bool {
        title == nil && game == nil && notes == nil && created == nil && modified == nil && videoURL == nil
    }
}

/// The timing model. Port of `src/crt/time.py` plus segment mode (spec §1.1).
/// A value type so undo/redo can snapshot it wholesale.
public struct TimeSession: Sendable, Equatable {
    public var startFrame: Int
    public var endFrame: Int
    public var framerate: Decimal
    public var precision: Int
    public var loads: [Load]
    public var mode: TimingMode
    public var segments: [Segment]
    public var meta: SessionMeta

    public init(startFrame: Int = 0, endFrame: Int = 0, framerate: Decimal = Decimal(60),
                precision: Int = 3, loads: [Load] = [], mode: TimingMode = .loads,
                segments: [Segment] = [], meta: SessionMeta = SessionMeta()) {
        self.startFrame = startFrame
        self.endFrame = endFrame
        self.framerate = framerate
        self.precision = precision
        self.loads = loads
        self.mode = mode
        self.segments = segments
        self.meta = meta
    }

    // MARK: - Frame lengths (loads mode)

    public var lengthWithLoads: Int { endFrame - startFrame }

    public var lengthWithoutLoads: Int {
        lengthWithLoads - loads.reduce(0) { $0 + $1.length }
    }

    /// Truncating average, matching Python's `int(sum / len)`.
    public var averageLoadLength: Int {
        guard !loads.isEmpty else { return 0 }
        return loads.reduce(0) { $0 + $1.length } / loads.count
    }

    // MARK: - Seconds (loads mode)

    /// Note: the Python implementation guards `int(framerate) == 0` here
    /// (a truncating cast) and `framerate == 0` for `withoutLoads`, so a
    /// sub-1 framerate zeroes the with-loads time only. Spec §1 overrides that
    /// quirk — "0 if framerate == 0 (never divide by zero)" — so both
    /// properties (and `segmentTotal`/`fullRun`) use the same `== 0` guard,
    /// matching the Windows port.
    public var withLoads: Decimal {
        if framerate == 0 { return Decimal(0) }
        return TimeFormatter.rounded(Decimal(lengthWithLoads) / framerate, scale: precision)
    }

    public var withoutLoads: Decimal {
        if framerate == 0 { return Decimal(0) }
        return TimeFormatter.rounded(Decimal(lengthWithoutLoads) / framerate, scale: precision)
    }

    // MARK: - Segment mode computations (spec §1.1)

    public var segmentTotalFrames: Int {
        segments.reduce(0) { $0 + $1.length }
    }

    /// (min start, max end) over all segments; (0, 0) when empty.
    public var segmentBounds: (start: Int, end: Int) {
        guard let first = segments.first else { return (0, 0) }
        var minStart = first.startFrame
        var maxEnd = first.endFrame
        for segment in segments.dropFirst() {
            minStart = min(minStart, segment.startFrame)
            maxEnd = max(maxEnd, segment.endFrame)
        }
        return (minStart, maxEnd)
    }

    public var fullRunFrames: Int {
        let bounds = segmentBounds
        return bounds.end - bounds.start
    }

    /// *Segment Total* — the sum of segment lengths, in seconds.
    public var segmentTotal: Decimal {
        if framerate == 0 { return Decimal(0) }
        return TimeFormatter.rounded(Decimal(segmentTotalFrames) / framerate, scale: precision)
    }

    /// *Full Run* — span from the earliest segment start to the latest end.
    public var fullRun: Decimal {
        if framerate == 0 { return Decimal(0) }
        return TimeFormatter.rounded(Decimal(fullRunFrames) / framerate, scale: precision)
    }

    // MARK: - Mode-aware display values (copy actions use these, spec §1.1)

    /// Loads mode: time without loads. Segment mode: segment total.
    public var displayWithoutLoads: Decimal {
        mode == .segments ? segmentTotal : withoutLoads
    }

    /// Loads mode: time with loads. Segment mode: full-run span.
    public var displayWithLoads: Decimal {
        mode == .segments ? fullRun : withLoads
    }

    /// The run's bounding frames regardless of mode (segment mode degrades to
    /// the segment bounds, matching the file format in spec §3).
    public var effectiveStartFrame: Int {
        mode == .segments ? segmentBounds.start : startFrame
    }

    public var effectiveEndFrame: Int {
        mode == .segments ? segmentBounds.end : endFrame
    }

    public func isoWithoutLoads() -> String { TimeFormatter.iso(displayWithoutLoads) }
    public func isoWithLoads() -> String { TimeFormatter.iso(displayWithLoads) }

    // MARK: - Validation (port of validate_load; spec: adding 0,0 → input error)

    static func validatePair(startFrame: Int, endFrame: Int) throws {
        if startFrame == endFrame {
            throw CRTError.loadZeroDuration
        }
        if startFrame > endFrame {
            throw CRTError.loadEndsBeforeStart
        }
    }

    // MARK: - Load mutation

    public mutating func addLoad(startFrame: Int, endFrame: Int) throws {
        if startFrame == 0 && endFrame == 0 {
            throw CRTError.loadInputRequired
        }
        try Self.validatePair(startFrame: startFrame, endFrame: endFrame)
        loads.append(Load(startFrame: startFrame, endFrame: endFrame))
    }

    public mutating func mutateLoad(at index: Int, startFrame: Int, endFrame: Int) throws {
        guard loads.indices.contains(index) else { return }
        try Self.validatePair(startFrame: startFrame, endFrame: endFrame)
        loads[index].startFrame = startFrame
        loads[index].endFrame = endFrame
    }

    public mutating func deleteLoad(at index: Int) {
        guard loads.indices.contains(index) else { return }
        loads.remove(at: index)
    }

    public mutating func clearLoads() {
        loads = []
    }

    // MARK: - Segment mutation (same rules as loads)

    public mutating func addSegment(startFrame: Int, endFrame: Int) throws {
        if startFrame == 0 && endFrame == 0 {
            throw CRTError.loadInputRequired
        }
        try Self.validatePair(startFrame: startFrame, endFrame: endFrame)
        segments.append(Segment(startFrame: startFrame, endFrame: endFrame))
    }

    public mutating func mutateSegment(at index: Int, startFrame: Int, endFrame: Int) throws {
        guard segments.indices.contains(index) else { return }
        try Self.validatePair(startFrame: startFrame, endFrame: endFrame)
        segments[index].startFrame = startFrame
        segments[index].endFrame = endFrame
    }

    public mutating func deleteSegment(at index: Int) {
        guard segments.indices.contains(index) else { return }
        segments.remove(at: index)
    }

    public mutating func clearSegments() {
        segments = []
    }

    // MARK: - Segment ↔ loads gap conversion (spec §3)

    /// The gaps between segments, expressed as loads, so segment-mode files
    /// degrade gracefully in the Python app: run bounds minus gaps equals the
    /// segment total (for non-overlapping segments).
    public func gapsBetweenSegments() -> [Load] {
        let sorted = segments.sorted { $0.startFrame < $1.startFrame }
        guard let first = sorted.first else { return [] }
        var gaps: [Load] = []
        var cursor = first.endFrame
        for segment in sorted.dropFirst() {
            if segment.startFrame > cursor {
                gaps.append(Load(startFrame: cursor, endFrame: segment.startFrame))
            }
            cursor = max(cursor, segment.endFrame)
        }
        return gaps
    }
}
