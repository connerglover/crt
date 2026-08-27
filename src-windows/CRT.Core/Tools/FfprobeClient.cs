using System.Globalization;
using System.Text.Json;

namespace CRT.Core.Tools;

/// <summary>Probed properties of a video file.</summary>
/// <param name="FpsRational">The exact avg_frame_rate rational, e.g. "30000/1001".</param>
/// <param name="Fps">The rational evaluated as a decimal, rounded to 3 places (29.97).</param>
public sealed record VideoInfo(int Width, int Height, string FpsRational, decimal Fps, decimal DurationSeconds);

/// <summary>Wraps <c>ffprobe -print_format json -show_streams -show_format</c>.</summary>
public sealed class FfprobeClient
{
    private readonly string _ffprobePath;

    public FfprobeClient(string ffprobePath)
    {
        _ffprobePath = ffprobePath;
    }

    public async Task<VideoInfo> ProbeAsync(string videoPath, CancellationToken ct = default)
    {
        var result = await ProcessRunner.RunAsync(
            _ffprobePath,
            new[] { "-v", "error", "-print_format", "json", "-show_streams", "-show_format", videoPath },
            ct).ConfigureAwait(false);

        if (!result.Success)
        {
            throw new InvalidOperationException(
                $"ffprobe failed ({result.ExitCode}): {FirstLine(result.StandardError)}");
        }

        using var document = JsonDocument.Parse(result.StandardOutput);
        var root = document.RootElement;

        JsonElement? videoStream = null;
        if (root.TryGetProperty("streams", out JsonElement streams) && streams.ValueKind == JsonValueKind.Array)
        {
            foreach (var stream in streams.EnumerateArray())
            {
                if (stream.TryGetProperty("codec_type", out JsonElement codecType) &&
                    codecType.GetString() == "video")
                {
                    videoStream = stream;
                    break;
                }
            }
        }
        if (videoStream is null)
        {
            throw new InvalidOperationException("The file has no video stream.");
        }

        var vs = videoStream.Value;
        int width = vs.TryGetProperty("width", out JsonElement w) ? w.GetInt32() : 0;
        int height = vs.TryGetProperty("height", out JsonElement h) ? h.GetInt32() : 0;

        string rational = vs.TryGetProperty("avg_frame_rate", out JsonElement afr)
            ? afr.GetString() ?? "0/1"
            : "0/1";
        if (rational is "0/0" or "0/1" && vs.TryGetProperty("r_frame_rate", out JsonElement rfr))
        {
            rational = rfr.GetString() ?? rational;
        }
        decimal fps = EvaluateRational(rational);

        decimal duration = 0m;
        if (vs.TryGetProperty("duration", out JsonElement streamDuration) &&
            decimal.TryParse(streamDuration.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out decimal sd))
        {
            duration = sd;
        }
        else if (root.TryGetProperty("format", out JsonElement format) &&
                 format.TryGetProperty("duration", out JsonElement formatDuration) &&
                 decimal.TryParse(formatDuration.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out decimal fd))
        {
            duration = fd;
        }

        return new VideoInfo(width, height, rational, fps, duration);
    }

    /// <summary>Evaluates "num/den" to a decimal rounded to 3 places (30000/1001 → 29.97).</summary>
    public static decimal EvaluateRational(string rational)
    {
        string[] parts = rational.Split('/');
        if (parts.Length == 2 &&
            decimal.TryParse(parts[0], NumberStyles.Number, CultureInfo.InvariantCulture, out decimal num) &&
            decimal.TryParse(parts[1], NumberStyles.Number, CultureInfo.InvariantCulture, out decimal den) &&
            den != 0m)
        {
            return Math.Round(num / den, 3, MidpointRounding.ToEven);
        }
        if (parts.Length == 1 &&
            decimal.TryParse(parts[0], NumberStyles.Number, CultureInfo.InvariantCulture, out decimal plain))
        {
            return plain;
        }
        return 0m;
    }

    private static string FirstLine(string text)
    {
        int newline = text.IndexOf('\n');
        return (newline >= 0 ? text[..newline] : text).Trim();
    }
}
