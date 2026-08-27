using System.Globalization;
using System.Text.RegularExpressions;

namespace CRT.Core.Tools;

/// <summary>
/// Runs the timer-overlay export:
/// <c>ffmpeg -y -ss trimStart -to trimEnd -i in -vf chain -c:v libx264 -preset veryfast -crf 18 -c:a aac -movflags +faststart out.mp4</c>
/// with progress parsed from stderr <c>time=</c> against the trimmed duration.
/// </summary>
public sealed partial class FfmpegExporter
{
    public const decimal LeadSeconds = 2m;
    public const decimal TailSeconds = 2m;

    private readonly string _ffmpegPath;

    public FfmpegExporter(string ffmpegPath)
    {
        _ffmpegPath = ffmpegPath;
    }

    [GeneratedRegex(@"time=(\d+):(\d+):(\d+(?:\.\d+)?)")]
    private static partial Regex TimeProgressRegex();

    /// <summary>Computes the export trim window: [runStart − lead, runEnd + tail], clamped to the video.</summary>
    public static (decimal TrimStart, decimal TrimEnd) ComputeTrim(decimal runStart, decimal runEnd, decimal videoDuration)
    {
        decimal trimStart = Math.Max(0m, runStart - LeadSeconds);
        decimal trimEnd = runEnd + TailSeconds;
        if (videoDuration > 0m)
        {
            trimEnd = Math.Min(trimEnd, videoDuration);
        }
        if (trimEnd <= trimStart)
        {
            trimEnd = trimStart + 1m;
        }
        return (trimStart, trimEnd);
    }

    /// <summary>Builds the full argv (after the executable) for the export.</summary>
    public static IReadOnlyList<string> BuildArguments(
        string inputPath, string outputPath, decimal trimStart, decimal trimEnd, string filtergraph)
    {
        return new[]
        {
            "-y",
            "-ss", TimerFiltergraphBuilder.Num(trimStart),
            "-to", TimerFiltergraphBuilder.Num(trimEnd),
            "-i", inputPath,
            "-vf", filtergraph,
            "-c:v", "libx264",
            "-preset", "veryfast",
            "-crf", "18",
            "-c:a", "aac",
            "-movflags", "+faststart",
            outputPath,
        };
    }

    public async Task ExportAsync(
        string inputPath,
        string outputPath,
        decimal trimStart,
        decimal trimEnd,
        string filtergraph,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        decimal duration = trimEnd - trimStart;

        var result = await ProcessRunner.RunAsync(
            _ffmpegPath,
            BuildArguments(inputPath, outputPath, trimStart, trimEnd, filtergraph),
            ct,
            onStdErrLine: line =>
            {
                var match = TimeProgressRegex().Match(line);
                if (!match.Success || duration <= 0m)
                {
                    return;
                }
                decimal seconds =
                    decimal.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) * 3600m +
                    decimal.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture) * 60m +
                    decimal.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
                progress?.Report(Math.Clamp((double)(seconds / duration), 0.0, 1.0));
            }).ConfigureAwait(false);

        if (!result.Success)
        {
            throw new InvalidOperationException($"ffmpeg failed ({result.ExitCode}): {TailOf(result.StandardError)}");
        }
        progress?.Report(1.0);
    }

    private static string TailOf(string stderr)
    {
        var lines = stderr.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return lines.Length > 0 ? lines[^1] : "unknown error";
    }
}
