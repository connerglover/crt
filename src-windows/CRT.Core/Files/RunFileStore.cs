using System.Globalization;
using System.Text;
using System.Text.Json;
using CRT.Core.Models;

namespace CRT.Core.Files;

/// <summary>
/// Reads/writes <c>*.json</c> run files. The on-disk format stays interchangeable
/// with the Python app: the four classic keys are always written first, in
/// Python <c>json.dump</c> style (<c>", "</c> / <c>": "</c> separators), and
/// segment-mode files degrade gracefully (bounds + gap-loads).
/// </summary>
public static class RunFileStore
{
    public const string CorruptFileMessage = "The file provided is corrupted.";

    // ── Serialization ──────────────────────────────────────────────────────

    /// <summary>Serializes a session to the on-disk JSON text.</summary>
    public static string Serialize(TimeSession session)
    {
        int startFrame, endFrame;
        List<Load> loads;
        if (session.Mode == TimingMode.Segments)
        {
            (startFrame, endFrame, loads) = SegmentMath.ToRunBoundsAndGaps(session.Segments);
        }
        else
        {
            startFrame = session.StartFrame;
            endFrame = session.EndFrame;
            loads = session.Loads;
        }

        var sb = new StringBuilder();
        sb.Append('{');
        sb.Append("\"start_frame\": ").Append(startFrame.ToString(CultureInfo.InvariantCulture));
        sb.Append(", \"end_frame\": ").Append(endFrame.ToString(CultureInfo.InvariantCulture));
        sb.Append(", \"framerate\": ").Append(JsonString(session.Framerate.ToString(CultureInfo.InvariantCulture)));
        sb.Append(", \"loads\": ").Append(FramePairs(loads.Select(l => (l.StartFrame, l.EndFrame))));
        sb.Append(", \"mode\": ").Append(JsonString(session.Mode.ToSerialString()));
        sb.Append(", \"segments\": ").Append(FramePairs(session.Segments.Select(s => (s.StartFrame, s.EndFrame))));
        sb.Append(", \"meta\": {");
        sb.Append("\"title\": ").Append(JsonString(session.Meta.Title));
        sb.Append(", \"game\": ").Append(JsonString(session.Meta.Game));
        sb.Append(", \"notes\": ").Append(JsonString(session.Meta.Notes));
        sb.Append(", \"created\": ").Append(JsonString(session.Meta.Created));
        sb.Append(", \"modified\": ").Append(JsonString(session.Meta.Modified));
        sb.Append(", \"video_url\": ").Append(JsonString(session.Meta.VideoUrl));
        sb.Append(", \"video_path\": ").Append(JsonString(session.Meta.VideoPath));
        sb.Append("}}");
        return sb.ToString();
    }

    /// <summary>Writes a session to disk, stamping meta.created/meta.modified.</summary>
    public static void Save(TimeSession session, string path)
    {
        string now = DateTimeOffset.Now.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture);
        if (string.IsNullOrEmpty(session.Meta.Created))
        {
            session.Meta.Created = now;
        }
        session.Meta.Modified = now;
        File.WriteAllText(path, Serialize(session));
    }

    // ── Deserialization ────────────────────────────────────────────────────

    /// <summary>Parses on-disk JSON text into a session. Corrupt input → <see cref="ValidationException"/>.</summary>
    public static TimeSession Deserialize(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new ValidationException(CorruptFileMessage);
            }

            var session = new TimeSession
            {
                StartFrame = root.GetProperty("start_frame").GetInt32(),
                EndFrame = root.GetProperty("end_frame").GetInt32(),
                Framerate = ReadFramerate(root.GetProperty("framerate")),
            };

            foreach (var pair in ReadFramePairs(root.GetProperty("loads")))
            {
                session.Loads.Add(new Load(pair.Item1, pair.Item2));
            }

            // New-format extras. A file with no "mode" is a plain Python file,
            // whose start/end/loads only mean anything in classic mode — so it
            // is pinned here rather than left to whatever the current default
            // happens to be. New sessions default to segments via settings;
            // letting that leak in here would silently reinterpret old runs.
            session.Mode = TimingMode.Loads;
            if (root.TryGetProperty("mode", out JsonElement modeElement) && modeElement.ValueKind == JsonValueKind.String)
            {
                session.Mode = TimingModeExtensions.ParseSerialString(modeElement.GetString());
            }
            if (root.TryGetProperty("segments", out JsonElement segmentsElement) && segmentsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var pair in ReadFramePairs(segmentsElement))
                {
                    session.Segments.Add(new Segment(pair.Item1, pair.Item2));
                }
            }
            if (session.Mode == TimingMode.Segments && session.Segments.Count == 0)
            {
                session.Mode = TimingMode.Loads;
            }
            if (root.TryGetProperty("meta", out JsonElement metaElement) && metaElement.ValueKind == JsonValueKind.Object)
            {
                session.Meta = new RunMeta
                {
                    Title = ReadString(metaElement, "title"),
                    Game = ReadString(metaElement, "game"),
                    Notes = ReadString(metaElement, "notes"),
                    Created = ReadString(metaElement, "created"),
                    Modified = ReadString(metaElement, "modified"),
                    VideoUrl = ReadString(metaElement, "video_url"),
                    VideoPath = ReadString(metaElement, "video_path"),
                };
            }

            return session;
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or FormatException or InvalidOperationException or OverflowException)
        {
            throw new ValidationException(CorruptFileMessage);
        }
    }

    /// <summary>Loads a session from disk. Corrupt content → <see cref="ValidationException"/>.</summary>
    public static TimeSession Load(string path) => Deserialize(File.ReadAllText(path));

    // ── Helpers ────────────────────────────────────────────────────────────

    private static decimal ReadFramerate(JsonElement element) => element.ValueKind switch
    {
        // Written as a string; accept a bare number too.
        JsonValueKind.String => decimal.Parse(element.GetString()!, NumberStyles.Number, CultureInfo.InvariantCulture),
        JsonValueKind.Number => element.GetDecimal(),
        _ => throw new ValidationException(CorruptFileMessage),
    };

    private static IEnumerable<(int, int)> ReadFramePairs(JsonElement array)
    {
        if (array.ValueKind != JsonValueKind.Array)
        {
            throw new ValidationException(CorruptFileMessage);
        }
        foreach (var entry in array.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Array || entry.GetArrayLength() < 2)
            {
                throw new ValidationException(CorruptFileMessage);
            }
            yield return (entry[0].GetInt32(), entry[1].GetInt32());
        }
    }

    private static string ReadString(JsonElement element, string property) =>
        element.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";

    private static string JsonString(string value) => JsonSerializer.Serialize(value);

    private static string FramePairs(IEnumerable<(int Start, int End)> pairs)
    {
        var parts = pairs.Select(p =>
            $"[{p.Start.ToString(CultureInfo.InvariantCulture)}, {p.End.ToString(CultureInfo.InvariantCulture)}]");
        return "[" + string.Join(", ", parts) + "]";
    }
}
