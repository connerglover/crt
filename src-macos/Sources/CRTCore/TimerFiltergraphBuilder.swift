import Foundation

public enum TimerCorner: String, Sendable, CaseIterable {
    case topLeft = "top-left"
    case topRight = "top-right"
    case bottomLeft = "bottom-left"
    case bottomRight = "bottom-right"

    /// drawtext x/y expressions with the 24px margin from spec §9.3.
    var positionOptions: String {
        switch self {
        case .topLeft: return "x=24:y=24"
        case .topRight: return "x=w-tw-24:y=24"
        case .bottomLeft: return "x=24:y=h-th-24"
        case .bottomRight: return "x=w-tw-24:y=h-th-24"
        }
    }
}

public enum TimerStyle: String, Sendable, CaseIterable {
    case pill
    case plain
}

public struct TimerOverlaySpec: Sendable, Equatable {
    public var videoHeight: Int
    public var corner: TimerCorner
    public var style: TimerStyle
    public var fontFile: String

    public init(videoHeight: Int,
                corner: TimerCorner = .bottomRight,
                style: TimerStyle = .pill,
                fontFile: String = "/System/Library/Fonts/Menlo.ttc") {
        self.videoHeight = videoHeight
        self.corner = corner
        self.style = style
        self.fontFile = fontFile
    }
}

/// Builds the piecewise drawtext chain for the LiveSplit-style timer overlay
/// (spec §9.3). All escaping is filter-level: every ':' inside a text value
/// or eif expression is '\:'; enable expressions are single-quoted. The whole
/// chain is passed to ffmpeg as ONE -vf argument (argument array, no shell).
public enum TimerFiltergraphBuilder {

    /// Trim window: [runStart − lead, runEnd + tail], clamped to the video.
    public static func trimBounds(runStart: Decimal, runEnd: Decimal,
                                  videoDuration: Decimal,
                                  lead: Decimal = Decimal(2),
                                  tail: Decimal = Decimal(2)) -> (start: Decimal, end: Decimal) {
        var start = runStart - lead
        if start < 0 { start = Decimal(0) }
        var end = runEnd + tail
        if videoDuration > 0 && end > videoDuration { end = videoDuration }
        if end < start { end = start }
        return (start, end)
    }

    /// Formats a seconds value for use inside filter expressions
    /// (max 3 decimals, plain '.' decimal string).
    static func seconds(_ value: Decimal) -> String {
        TimeFormatter.string(TimeFormatter.rounded(value, scale: 3))
    }

    /// A constant clock string "HH\:MM\:SS.mmm" (filter-escaped colons).
    static func constantClock(_ elapsed: Decimal) -> String {
        let clamped = elapsed < 0 ? Decimal(0) : elapsed
        // Floor, like the `trunc(...)` in `runningClock` and the Windows
        // builder — a frozen clock must never read ahead of the running one.
        let totalMs = TimeFormatter.truncatedToInt(clamped * Decimal(1000))
        let hours = totalMs / 3_600_000
        let minutes = (totalMs % 3_600_000) / 60_000
        let secs = (totalMs % 60_000) / 1000
        let ms = totalMs % 1000
        return String(format: "%02d\\:%02d\\:%02d.%03d", hours, minutes, secs, ms)
    }

    /// The running clock eif expression for a window with offset `o`
    /// (the timer shows t − o).
    static func runningClock(offset: Decimal) -> String {
        let o = seconds(offset)
        return "%{eif\\:trunc((t-\(o))/3600)\\:d\\:2}\\:"
            + "%{eif\\:trunc(mod((t-\(o))/60,60))\\:d\\:2}\\:"
            + "%{eif\\:trunc(mod(t-\(o),60))\\:d\\:2}."
            + "%{eif\\:trunc(mod((t-\(o))*1000,1000))\\:d\\:3}"
    }

    /// The style/position options shared by every drawtext in the chain.
    static func styleOptions(spec: TimerOverlaySpec) -> String {
        let fontSize = max(spec.videoHeight / 18, 8)
        // ffmpeg unescapes filter arguments twice: the surrounding quotes are
        // consumed by the graph parser, so a raw ':' in the path would still
        // split the drawtext options. Escape it like the Windows builder does.
        let font = spec.fontFile.replacingOccurrences(of: ":", with: "\\:")
        var options = "fontfile='\(font)':fontsize=\(fontSize):fontcolor=white"
        if spec.style == .pill {
            options += ":box=1:boxcolor=black@0.55:boxborderw=10"
        }
        options += ":" + spec.corner.positionOptions
        return options
    }

    static func drawtext(spec: TimerOverlaySpec, enable: String, text: String) -> String {
        "drawtext=\(styleOptions(spec: spec)):enable='\(enable)':text='\(text)'"
    }

    /// Builds the full chain.
    ///
    /// - Parameters:
    ///   - runStart/runEnd: run bounds in source-video seconds.
    ///   - pauses: frozen windows (loads, or gaps between segments) in
    ///     source-video seconds. They are clamped to the run and sorted.
    ///   - trimStart: the trim start passed to `-ss`; all window times are
    ///     emitted relative to it.
    public static func build(runStart: Decimal, runEnd: Decimal,
                             pauses: [(start: Decimal, end: Decimal)],
                             trimStart: Decimal,
                             spec: TimerOverlaySpec) -> String {
        // Relative bounds.
        var r0 = runStart - trimStart
        if r0 < 0 { r0 = Decimal(0) }
        var r1 = runEnd - trimStart
        if r1 < r0 { r1 = r0 }

        // Normalize pauses: clamp into the run, drop empties, sort.
        var windows: [(start: Decimal, end: Decimal)] = []
        for pause in pauses {
            var start = pause.start - trimStart
            var end = pause.end - trimStart
            if start < r0 { start = r0 }
            if end > r1 { end = r1 }
            if end > start {
                windows.append((start, end))
            }
        }
        windows.sort { $0.start < $1.start }

        var filters: [String] = []

        // Before the run: constant zero.
        if r0 > 0 {
            filters.append(drawtext(spec: spec,
                                    enable: "lt(t,\(seconds(r0)))",
                                    text: constantClock(Decimal(0))))
        }

        var cursor = r0
        var accumulatedPause = Decimal(0)

        for window in windows {
            if window.start > cursor {
                // Running window [cursor, window.start).
                let offset = r0 + accumulatedPause
                filters.append(drawtext(spec: spec,
                                        enable: "between(t,\(seconds(cursor)),\(seconds(window.start)))",
                                        text: runningClock(offset: offset)))
            }
            // Frozen window [window.start, window.end).
            let frozenElapsed = window.start - r0 - accumulatedPause
            filters.append(drawtext(spec: spec,
                                    enable: "between(t,\(seconds(window.start)),\(seconds(window.end)))",
                                    text: constantClock(frozenElapsed)))
            accumulatedPause += window.end - window.start
            cursor = window.end
        }

        // Final running window.
        if r1 > cursor {
            let offset = r0 + accumulatedPause
            filters.append(drawtext(spec: spec,
                                    enable: "between(t,\(seconds(cursor)),\(seconds(r1)))",
                                    text: runningClock(offset: offset)))
        }

        // After the run: hold the final time.
        let finalElapsed = r1 - r0 - accumulatedPause
        filters.append(drawtext(spec: spec,
                                enable: "gt(t,\(seconds(r1)))",
                                text: constantClock(finalElapsed)))

        return filters.joined(separator: ",")
    }
}
