using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CRT.Core.Models;

namespace CRT.Core.Formatting;

/// <summary>
/// Builds the mod note from the user's template — ported from the
/// <c>_mod_note</c> property in <c>src/crt/app/app.py</c>. Unknown placeholders
/// are left literal instead of raising.
/// </summary>
public static partial class ModNoteBuilder
{
    public const string Plug =
        "[Conner's Retime Tool](https://github.com/connerglover/conners-retime-tool)";

    public const string DefaultTemplate = "Mod Note: Retimed to {time_without_loads}";

    [GeneratedRegex(@"\{([a-zA-Z_][a-zA-Z0-9_]*)\}")]
    private static partial Regex PlaceholderRegex();

    public static string Build(TimeSession session, string template)
    {
        var values = BuildValues(session);
        return PlaceholderRegex().Replace(template, match =>
            values.TryGetValue(match.Groups[1].Value, out string? value) ? value : match.Value);
    }

    private static Dictionary<string, string> BuildValues(TimeSession session)
    {
        decimal fps = session.Framerate;

        string startTime, endTime;
        if (fps == 0m)
        {
            startTime = "0";
            endTime = "0";
        }
        else
        {
            startTime = FormatSeconds(Math.Round(session.EffectiveStartFrame / fps, session.Precision, MidpointRounding.ToEven));
            endTime = FormatSeconds(Math.Round(session.EffectiveEndFrame / fps, session.Precision, MidpointRounding.ToEven));
        }

        var (hours, minutes, seconds, milliseconds) = TimeFormatter.FormatComponents(session.SecondarySeconds);

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["time_with_loads"] = TimeFormatter.FormatIso(session.SecondarySeconds),
            ["time_without_loads"] = TimeFormatter.FormatIso(session.PrimarySeconds),
            ["hours"] = hours,
            ["minutes"] = minutes,
            ["seconds"] = seconds,
            ["milliseconds"] = milliseconds,
            ["start_frame"] = session.EffectiveStartFrame.ToString(CultureInfo.InvariantCulture),
            ["end_frame"] = session.EffectiveEndFrame.ToString(CultureInfo.InvariantCulture),
            ["start_time"] = startTime,
            ["end_time"] = endTime,
            ["total_frames"] = session.EffectiveTotalFrames.ToString(CultureInfo.InvariantCulture),
            ["fps"] = fps.ToString(CultureInfo.InvariantCulture),
            ["plug"] = Plug,
        };
    }

    /// <summary>
    /// Formats a rounded seconds value the way Python's float str() renders it in
    /// the mod note: trailing zeros trimmed, but at least one fractional digit.
    /// </summary>
    private static string FormatSeconds(decimal value)
    {
        string text = value.ToString(CultureInfo.InvariantCulture);
        if (text.Contains('.'))
        {
            text = text.TrimEnd('0');
            if (text.EndsWith('.'))
            {
                text += "0";
            }
        }
        else
        {
            text += ".0";
        }
        return text;
    }
}
