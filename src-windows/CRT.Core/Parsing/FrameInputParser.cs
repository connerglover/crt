using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using CRT.Core.Models;

namespace CRT.Core.Parsing;

/// <summary>Frame/framerate input parsing ported from <c>src/crt/frame_input.py</c>.</summary>
public static partial class FrameInputParser
{
    public const string InvalidDebugInfoMessage =
        "The debug info provided is invalid.\nPlease re-enter debug info.";

    [GeneratedRegex("[^0-9.]")]
    private static partial Regex NonNumericRegex();

    [GeneratedRegex("[0-9]")]
    private static partial Regex AnyDigitRegex();

    /// <summary>True if the text looks like YouTube debug-info JSON.</summary>
    public static bool IsDebugInfo(string text) =>
        text.Contains('{') && text.Contains("\"cmt\"");

    /// <summary>Converts YouTube debug-info JSON to a frame number.</summary>
    public static int DebugInfoToFrame(decimal framerate, string debugInfo)
    {
        int startPos = debugInfo.IndexOf('{');
        if (startPos == -1)
        {
            throw new ValidationException(InvalidDebugInfoMessage);
        }

        decimal cmt;
        try
        {
            using var document = JsonDocument.Parse(debugInfo[startPos..]);
            if (!document.RootElement.TryGetProperty("cmt", out JsonElement cmtElement))
            {
                throw new ValidationException(InvalidDebugInfoMessage);
            }
            cmt = cmtElement.ValueKind switch
            {
                JsonValueKind.String => decimal.Parse(cmtElement.GetString()!, CultureInfo.InvariantCulture),
                JsonValueKind.Number => cmtElement.GetDecimal(),
                _ => throw new ValidationException(InvalidDebugInfoMessage),
            };
        }
        catch (Exception e) when (e is JsonException or FormatException or OverflowException)
        {
            throw new ValidationException(InvalidDebugInfoMessage);
        }

        return (int)Math.Round(cmt * framerate, 0, MidpointRounding.ToEven);
    }

    /// <summary>
    /// Cleans a framerate string into a valid decimal: strip non-[0-9.], no
    /// digits → 0, collapse extra dots, trailing dot gets "0" appended.
    /// </summary>
    public static decimal CleanFramerate(string framerate)
    {
        string cleaned = NonNumericRegex().Replace(framerate, "");
        if (!AnyDigitRegex().IsMatch(cleaned))
        {
            return 0m;
        }
        cleaned = CollapseDots(cleaned);
        if (cleaned.EndsWith('.'))
        {
            cleaned += "0";
        }
        if (decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal value))
        {
            return value;
        }
        return 0m;
    }

    /// <summary>
    /// Parses a frame input field:
    /// 1. YouTube debug info → frame from JSON "cmt".
    /// 2. Else strip all non-[0-9.] characters.
    /// 3. Empty → 0.
    /// 4. A dot remains → value is seconds: round(value × framerate).
    /// 5. Else plain integer.
    /// </summary>
    public static int ParseFrameInput(string text, decimal framerate)
    {
        text = text.Trim();

        if (IsDebugInfo(text))
        {
            return DebugInfoToFrame(framerate, text);
        }

        string cleaned = NonNumericRegex().Replace(text, "");

        if (cleaned.Length == 0 || !AnyDigitRegex().IsMatch(cleaned))
        {
            return 0;
        }

        cleaned = CollapseDots(cleaned);

        if (cleaned.Contains('.'))
        {
            if (framerate == 0m)
            {
                return 0;
            }
            if (!decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal seconds))
            {
                return 0;
            }
            return (int)Math.Round(seconds * framerate, 0, MidpointRounding.ToEven);
        }

        if (int.TryParse(cleaned, NumberStyles.Integer, CultureInfo.InvariantCulture, out int frame))
        {
            return frame;
        }
        return 0;
    }

    /// <summary>Collapses multiple decimal points, keeping only the first.</summary>
    private static string CollapseDots(string text)
    {
        int first = text.IndexOf('.');
        if (first == -1 || text.IndexOf('.', first + 1) == -1)
        {
            return text;
        }
        return text[..(first + 1)] + text[(first + 1)..].Replace(".", "");
    }
}
