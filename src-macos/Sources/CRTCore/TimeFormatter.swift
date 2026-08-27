import Foundation

/// Decimal helpers plus all time formatting, ported from `src/crt/decorators.py`.
///
/// Every time value in CRT is a Foundation `Decimal` so values such as 29.97
/// behave exactly. Rounding uses `NSDecimalRound` with `.bankers` (round half
/// to even) because that is what Python's `round(Decimal, n)` does by default
/// (`decimal.ROUND_HALF_EVEN`) and what the Windows port spells out as
/// `MidpointRounding.ToEven` — all three apps must agree on midpoints.
public enum TimeFormatter {

    // MARK: - Decimal helpers

    /// Rounds a decimal to `scale` fractional digits (round half to even,
    /// matching Python's default decimal context).
    public static func rounded(_ value: Decimal, scale: Int) -> Decimal {
        var input = value
        var result = Decimal()
        NSDecimalRound(&result, &input, scale, .bankers)
        return result
    }

    /// Rounds a decimal to the nearest integer and returns it as `Int`.
    public static func roundedToInt(_ value: Decimal) -> Int {
        let r = rounded(value, scale: 0)
        return NSDecimalNumber(decimal: r).intValue
    }

    /// Truncates a decimal toward zero and returns it as `Int`
    /// (the equivalent of Python's `int(Decimal)` cast).
    public static func truncatedToInt(_ value: Decimal) -> Int {
        var input = value
        var result = Decimal()
        NSDecimalRound(&result, &input, 0, value < 0 ? .up : .down)
        return NSDecimalNumber(decimal: result).intValue
    }

    /// Locale-independent plain string for a decimal (always '.' separator).
    public static func string(_ value: Decimal) -> String {
        return NSDecimalNumber(decimal: value).stringValue
    }

    // MARK: - Components (port of format_components)

    /// Numeric time components. Negative input clamps to zero.
    /// The milliseconds part is returned as a ready-made 3-digit string built
    /// by splitting the decimal string representation, exactly like the
    /// Python implementation does for precision-3 values.
    static func numericComponents(_ time: Decimal) -> (hours: Int, minutes: Int, seconds: Int, milliseconds: String) {
        let clamped = time < 0 ? Decimal(0) : time
        let text = string(clamped)

        let secondsPart: String
        var fractionPart: String
        if let dot = text.firstIndex(of: ".") {
            secondsPart = String(text[text.startIndex..<dot])
            fractionPart = String(text[text.index(after: dot)...])
        } else {
            secondsPart = text
            fractionPart = ""
        }

        // The fractional digits are positional: "05" means 50 ms. Pad on the
        // right to three digits — and cap at three, so a value carrying more
        // precision than the display format still renders exactly three
        // millisecond digits (spec §5).
        if fractionPart.count > 3 {
            fractionPart = String(fractionPart.prefix(3))
        } else {
            while fractionPart.count < 3 {
                fractionPart += "0"
            }
        }

        let totalSeconds = Int(secondsPart) ?? 0
        let hours = totalSeconds / 3600
        let minutes = (totalSeconds % 3600) / 60
        let seconds = totalSeconds % 60
        return (hours, minutes, seconds, fractionPart)
    }

    /// String components in the Python `format_components` style:
    /// hours/minutes/seconds zero-padded to 2 digits, milliseconds 3 digits.
    public static func components(_ time: Decimal) -> (hours: String, minutes: String, seconds: String, milliseconds: String) {
        let (h, m, s, ms) = numericComponents(time)
        return (
            String(format: "%02d", h),
            String(format: "%02d", m),
            String(format: "%02d", s),
            ms
        )
    }

    // MARK: - ISO display format (port of format_iso)

    /// Drops leading zero *units*, but every unit that is shown is two digits:
    /// `SS.mmm` under a minute (zero state `"00.000"`), `MM:SS.mmm` under an
    /// hour (60s → `"01:00.000"`), else `HH:MM:SS.mmm` (3600s → `"01:00:00.000"`).
    /// Negative values clamp to zero. Milliseconds are always 3 digits.
    ///
    /// The leading unit stays zero-padded because the Python app interpolates
    /// its already-padded component strings, and mod notes produced here are
    /// pasted into speedrun.com by moderators — the apps must agree byte for
    /// byte. Verified against `src/crt/decorators.py`'s `format_iso`.
    public static func iso(_ time: Decimal) -> String {
        let (h, m, s, ms) = numericComponents(time)
        if h > 0 {
            return String(format: "%02d:%02d:%02d.", h, m, s) + ms
        }
        if m > 0 {
            return String(format: "%02d:%02d.", m, s) + ms
        }
        return String(format: "%02d.", s) + ms
    }

    /// Converts a frame count/position at the given framerate into an
    /// ISO-style timestamp. `"00.000"` when the framerate is zero.
    public static func frameTime(frames: Int, framerate: Decimal, precision: Int) -> String {
        if framerate == 0 {
            return iso(Decimal(0))
        }
        let seconds = rounded(Decimal(frames) / framerate, scale: precision)
        return iso(seconds)
    }

    /// Formats an absolute frame position as a YouTube chapter timestamp
    /// (`M:SS` or `H:MM:SS`) — chapters do not support milliseconds, the
    /// value floors to whole seconds.
    public static func youtubeTimestamp(frame: Int, framerate: Decimal) -> String {
        let totalSeconds: Int
        if framerate == 0 {
            totalSeconds = 0
        } else {
            totalSeconds = truncatedToInt(Decimal(frame) / framerate)
        }
        let hours = totalSeconds / 3600
        let minutes = (totalSeconds % 3600) / 60
        let seconds = totalSeconds % 60
        if hours > 0 {
            return String(format: "%d:%02d:%02d", hours, minutes, seconds)
        }
        return String(format: "%d:%02d", minutes, seconds)
    }

    /// Speedrun.com style: `"HHh MMm SSs mmmms"` (port of src_format).
    public static func srcFormat(_ time: Decimal) -> String {
        let (h, m, s, ms) = components(time)
        return "\(h)h \(m)m \(s)s \(ms)ms"
    }
}
