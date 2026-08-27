using System.Globalization;
using CRT.Core.Models;

namespace CRT.Core.Formatting;

/// <summary>
/// Time formatting ported from <c>src/crt/decorators.py</c>
/// (<c>format_components</c> / <c>format_iso</c> / <c>format_frame_time</c>).
/// </summary>
public static class TimeFormatter
{
    /// <summary>
    /// Splits a decimal seconds value into ("HH", "MM", "SS", "mmm") strings by
    /// decimal string splitting, exactly like the Python <c>format_components</c>.
    /// Negative values clamp to 0. Milliseconds are always 3 digits.
    /// </summary>
    public static (string Hours, string Minutes, string Seconds, string Milliseconds) FormatComponents(decimal time)
    {
        decimal clamped = time < 0m ? 0m : time;
        string timeStr = clamped.ToString(CultureInfo.InvariantCulture);

        long totalSeconds;
        string fraction;
        int dot = timeStr.IndexOf('.');
        if (dot >= 0)
        {
            totalSeconds = long.Parse(timeStr[..dot], CultureInfo.InvariantCulture);
            fraction = timeStr[(dot + 1)..];
        }
        else
        {
            totalSeconds = long.Parse(timeStr, CultureInfo.InvariantCulture);
            fraction = "";
        }

        // The Python code receives values quantized to exactly 3 decimal places
        // and does str(int(frac)).rjust(3, "0"). Normalizing the fractional digit
        // string to exactly 3 digits is equivalent for those values and also
        // correct for shorter-scale decimals (e.g. "65.1" → "100").
        string ms = fraction.Length >= 3 ? fraction[..3] : fraction.PadRight(3, '0');

        long minutes = Math.DivRem(totalSeconds, 60, out long seconds);
        long hours = Math.DivRem(minutes, 60, out minutes);

        return (
            hours.ToString("00", CultureInfo.InvariantCulture),
            minutes.ToString("00", CultureInfo.InvariantCulture),
            seconds.ToString("00", CultureInfo.InvariantCulture),
            ms);
    }

    /// <summary>
    /// ISO-style display format: drops leading zero <em>units</em>, but every unit
    /// that is shown is two digits — "SS.mmm" under a minute ("00.000" zero-state),
    /// "MM:SS.mmm" under an hour (60s → "01:00.000"), else "HH:MM:SS.mmm"
    /// (3600s → "01:00:00.000"). Negative clamps to 0.
    /// </summary>
    /// <remarks>
    /// The leading unit stays zero-padded because the Python app interpolates the
    /// already-padded component strings, and mod notes produced here are pasted
    /// into speedrun.com by moderators — the two apps must agree byte for byte.
    /// Verified against <c>src/crt/decorators.py</c>'s <c>format_iso</c>.
    /// </remarks>
    public static string FormatIso(decimal time)
    {
        var (hours, minutes, seconds, ms) = FormatComponents(time);
        long h = long.Parse(hours, CultureInfo.InvariantCulture);
        long m = long.Parse(minutes, CultureInfo.InvariantCulture);
        if (h > 0)
        {
            return $"{hours}:{minutes}:{seconds}.{ms}";
        }
        if (m > 0)
        {
            return $"{minutes}:{seconds}.{ms}";
        }
        return $"{seconds}.{ms}";
    }

    /// <summary>
    /// Converts a frame count/position at the given framerate into an ISO-style
    /// timestamp; "00.000" when framerate is 0.
    /// </summary>
    public static string FormatFrameTime(int frames, decimal framerate, int precision)
    {
        if (framerate == 0m)
        {
            return FormatIso(0m);
        }
        return FormatIso(Math.Round(frames / framerate, precision, MidpointRounding.ToEven));
    }

    /// <summary>
    /// YouTube chapter timestamp (M:SS or H:MM:SS — no milliseconds, floored to seconds).
    /// </summary>
    public static string FormatYouTubeTimestamp(int frame, decimal framerate)
    {
        long totalSeconds = framerate == 0m ? 0 : (long)(frame / framerate);
        long hours = Math.DivRem(totalSeconds, 3600, out long remainder);
        long minutes = Math.DivRem(remainder, 60, out long seconds);
        if (hours > 0)
        {
            return $"{hours}:{minutes:00}:{seconds:00}";
        }
        return $"{minutes}:{seconds:00}";
    }

    /// <summary>ISO display for the session's primary time ("without loads" / segment total).</summary>
    public static string PrimaryIso(TimeSession session) => FormatIso(session.PrimarySeconds);

    /// <summary>ISO display for the session's secondary time ("with loads" / full run).</summary>
    public static string SecondaryIso(TimeSession session) => FormatIso(session.SecondarySeconds);
}
