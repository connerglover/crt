using System.Text.Json;

namespace CRT.Core.Parsing;

/// <summary>
/// Extraction of (videoId, itag) pairs from pasted YouTube debug info —
/// ported from <c>extract_debug_info_ids</c> in <c>src/crt/youtube_format.py</c>.
/// </summary>
public static class DebugInfo
{
    /// <summary>
    /// Extracts ("docid", "fmt") from a YouTube debug info blob. Returns null
    /// if the text isn't parseable JSON or is missing either field. The "fmt"
    /// itag is returned as a string regardless of its JSON type.
    /// </summary>
    public static (string VideoId, string FormatId)? ExtractIds(string debugInfo)
    {
        int startPos = debugInfo.IndexOf('{');
        if (startPos == -1)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(debugInfo[startPos..]);
            var root = document.RootElement;
            string? videoId = ReadAsString(root, "docid");
            string? formatId = ReadAsString(root, "fmt");
            if (string.IsNullOrEmpty(videoId) || string.IsNullOrEmpty(formatId))
            {
                return null;
            }
            return (videoId, formatId);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ReadAsString(JsonElement root, string property)
    {
        if (!root.TryGetProperty(property, out JsonElement element))
        {
            return null;
        }
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.GetRawText(),
            _ => null,
        };
    }
}
