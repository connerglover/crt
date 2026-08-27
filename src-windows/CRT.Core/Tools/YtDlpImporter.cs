using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CRT.Core.Tools;

/// <summary>
/// Imports YouTube videos through yt-dlp: cached download with progress parsed
/// from stdout, plus fps lookup via <c>yt-dlp -j</c>.
/// </summary>
public sealed partial class YtDlpImporter
{
    /// <summary>The download format selection from the spec (≤1080p mp4).</summary>
    public const string FormatSelector =
        "bv*[height<=1080][ext=mp4]+ba[ext=m4a]/b[height<=1080][ext=mp4]/b";

    private readonly string _ytDlpPath;
    private readonly string _cacheDirectory;

    public YtDlpImporter(string ytDlpPath, string cacheDirectory)
    {
        _ytDlpPath = ytDlpPath;
        _cacheDirectory = cacheDirectory;
    }

    [GeneratedRegex(@"(?:youtube\.com/(?:watch\?(?:.*&)?v=|shorts/|live/|embed/)|youtu\.be/)([A-Za-z0-9_-]{6,20})", RegexOptions.IgnoreCase)]
    private static partial Regex VideoIdRegex();

    [GeneratedRegex(@"\[download\]\s+(\d+(?:\.\d+)?)%")]
    private static partial Regex DownloadProgressRegex();

    /// <summary>True when the URL looks like a YouTube watch/shorts/youtu.be link.</summary>
    public static bool IsYouTubeUrl(string url) => VideoIdRegex().IsMatch(url);

    /// <summary>Extracts the video id, or null when the URL isn't recognizably YouTube.</summary>
    public static string? ExtractVideoId(string url)
    {
        var match = VideoIdRegex().Match(url);
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>
    /// Downloads a YouTube URL into the cache (re-using an existing cached file
    /// for the same id) and returns the local path.
    /// </summary>
    public async Task<string> DownloadAsync(string url, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        Directory.CreateDirectory(_cacheDirectory);

        string? videoId = ExtractVideoId(url);
        if (videoId is not null)
        {
            string cached = Path.Combine(_cacheDirectory, videoId + ".mp4");
            if (File.Exists(cached))
            {
                progress?.Report(1.0);
                return cached;
            }
        }

        var result = await ProcessRunner.RunAsync(
            _ytDlpPath,
            new[]
            {
                "-f", FormatSelector,
                "--merge-output-format", "mp4",
                "-o", Path.Combine(_cacheDirectory, "%(id)s.%(ext)s"),
                url,
            },
            ct,
            onStdOutLine: line =>
            {
                var match = DownloadProgressRegex().Match(line);
                if (match.Success &&
                    double.TryParse(match.Groups[1].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out double percent))
                {
                    progress?.Report(percent / 100.0);
                }
            }).ConfigureAwait(false);

        if (!result.Success)
        {
            throw new InvalidOperationException($"yt-dlp failed ({result.ExitCode}): {LastLine(result.StandardError)}");
        }

        if (videoId is not null)
        {
            string expected = Path.Combine(_cacheDirectory, videoId + ".mp4");
            if (File.Exists(expected))
            {
                return expected;
            }
        }

        // Fall back to the newest file in the cache (id unknown or non-mp4 ext).
        var newest = new DirectoryInfo(_cacheDirectory)
            .GetFiles()
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .FirstOrDefault();
        return newest?.FullName
            ?? throw new InvalidOperationException("yt-dlp reported success but produced no file.");
    }

    /// <summary>
    /// Reads the video metadata JSON (<c>yt-dlp -j</c>). When
    /// <paramref name="formatId"/> is given, returns that format's fps;
    /// otherwise the top-level fps.
    /// </summary>
    public async Task<decimal?> GetFpsAsync(string url, string? formatId = null, CancellationToken ct = default)
    {
        var result = await ProcessRunner.RunAsync(_ytDlpPath, new[] { "-j", url }, ct).ConfigureAwait(false);
        if (!result.Success)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(result.StandardOutput);
            var root = document.RootElement;

            if (formatId is not null &&
                root.TryGetProperty("formats", out JsonElement formats) &&
                formats.ValueKind == JsonValueKind.Array)
            {
                foreach (var format in formats.EnumerateArray())
                {
                    if (format.TryGetProperty("format_id", out JsonElement id) &&
                        id.GetString() == formatId &&
                        format.TryGetProperty("fps", out JsonElement formatFps) &&
                        formatFps.ValueKind == JsonValueKind.Number)
                    {
                        return formatFps.GetDecimal();
                    }
                }
                return null;
            }

            if (root.TryGetProperty("fps", out JsonElement fps) && fps.ValueKind == JsonValueKind.Number)
            {
                return fps.GetDecimal();
            }
        }
        catch (JsonException)
        {
            // Fall through to null.
        }
        return null;
    }

    /// <summary>yt-dlp fps fallback for the innertube framerate check (by video id + itag).</summary>
    public Task<decimal?> GetFpsByVideoIdAsync(string videoId, string formatId, CancellationToken ct = default) =>
        GetFpsAsync($"https://www.youtube.com/watch?v={videoId}", formatId, ct);

    private static string LastLine(string text)
    {
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return lines.Length > 0 ? lines[^1] : "";
    }
}
