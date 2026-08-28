using System.Globalization;
using System.Text.RegularExpressions;

namespace CRT.Core.Tools;

/// <summary>
/// Runs the timer-overlay export:
/// <c>ffmpeg -y -ss trimStart -to trimEnd -i in -filter_complex graph -map [v] -map 0:a? -c:v ENCODER QUALITY -c:a aac -movflags +faststart out.mp4</c>
/// with progress parsed from stderr <c>time=</c> against the trimmed duration.
/// The encoder is whichever <see cref="VideoEncoderCatalog"/> finds, since
/// libx264 is missing from LGPL ffmpeg builds.
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
    /// <remarks>
    /// filter_complex rather than -vf, because a rounded background is a second
    /// input overlaid under the text and -vf only takes one. The graph is
    /// expected to end in [v]; audio is mapped optionally so a silent clip does
    /// not fail the export.
    /// </remarks>
    public static IReadOnlyList<string> BuildArguments(
        string inputPath, string outputPath, decimal trimStart, decimal trimEnd, string filterGraph,
        VideoEncoder? encoder = null)
    {
        encoder ??= VideoEncoderCatalog.Default;
        var arguments = new List<string>
        {
            "-y",
            "-ss", TimerFiltergraphBuilder.Num(trimStart),
            "-to", TimerFiltergraphBuilder.Num(trimEnd),
            "-i", inputPath,
            "-filter_complex", filterGraph,
            "-map", "[v]",
            "-map", "0:a?",
            "-c:v", encoder.Name,
        };
        arguments.AddRange(encoder.QualityArguments);
        arguments.AddRange(new[]
        {
            "-c:a", "aac",
            "-movflags", "+faststart",
            outputPath,
        });
        return arguments;
    }

    /// <summary>
    /// The encoder this ffmpeg will be asked to use, detected once and reused.
    /// </summary>
    public async Task<VideoEncoder?> ResolveEncoderAsync(CancellationToken ct = default) =>
        _encoder ??= await VideoEncoderCatalog.DetectAsync(_ffmpegPath, ct).ConfigureAwait(false);

    private VideoEncoder? _encoder;

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

        // Resolved before the export rather than after it fails: an encode can
        // run for minutes before ffmpeg would report an unknown encoder.
        var encoder = await ResolveEncoderAsync(ct).ConfigureAwait(false);
        if (encoder is null)
        {
            throw new InvalidOperationException(NoEncoderMessage);
        }

        var result = await ProcessRunner.RunAsync(
            _ffmpegPath,
            BuildArguments(inputPath, outputPath, trimStart, trimEnd, filtergraph, encoder),
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

    /// <summary>
    /// Shown when no candidate encoder exists, which in practice means an ffmpeg
    /// built without any of them — so the message says what to do about it.
    /// </summary>
    public const string NoEncoderMessage =
        "This copy of ffmpeg has no usable video encoder. " +
        "Clear the FFmpeg path in Settings to let CRT download a build that does.";

    private static string TailOf(string stderr)
    {
        var lines = stderr.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return lines.Length > 0 ? lines[^1] : "unknown error";
    }
}
