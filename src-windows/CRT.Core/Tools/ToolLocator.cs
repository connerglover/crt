using System.IO.Compression;

namespace CRT.Core.Tools;

public enum ToolKind
{
    Ffmpeg,
    Ffprobe,
    YtDlp,
}

/// <summary>
/// Locates (and, when missing, downloads) ffmpeg / ffprobe / yt-dlp:
/// 1. explicit path from settings, 2. the config tools dir, 3. PATH lookup,
/// 4. download into the tools dir.
/// </summary>
public sealed class ToolLocator
{
    public const string FfmpegDownloadUrl =
        "https://github.com/BtbN/FFmpeg-Builds/releases/latest/download/ffmpeg-master-latest-win64-gpl.zip";

    public const string YtDlpDownloadUrl =
        "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";

    /// <summary>Approximate download sizes, for the download prompt.</summary>
    public const string FfmpegApproxSize = "180 MB";
    public const string YtDlpApproxSize = "18 MB";

    private readonly string _toolsDirectory;
    private readonly Func<string> _ffmpegSetting;
    private readonly Func<string> _ytDlpSetting;
    private readonly HttpClient _http;

    public ToolLocator(string toolsDirectory, Func<string> ffmpegSetting, Func<string> ytDlpSetting, HttpClient? http = null)
    {
        _toolsDirectory = toolsDirectory;
        _ffmpegSetting = ffmpegSetting;
        _ytDlpSetting = ytDlpSetting;
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
    }

    public static string ExecutableName(ToolKind kind) => kind switch
    {
        ToolKind.Ffmpeg => "ffmpeg.exe",
        ToolKind.Ffprobe => "ffprobe.exe",
        ToolKind.YtDlp => "yt-dlp.exe",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    public static string DisplayName(ToolKind kind) => kind switch
    {
        ToolKind.Ffmpeg => "FFmpeg",
        ToolKind.Ffprobe => "ffprobe",
        ToolKind.YtDlp => "yt-dlp",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    public static string ApproxDownloadSize(ToolKind kind) =>
        kind == ToolKind.YtDlp ? YtDlpApproxSize : FfmpegApproxSize;

    /// <summary>
    /// Directories a build may ship tools in, next to the executable.
    /// </summary>
    /// <remarks>
    /// ffmpeg and yt-dlp are separate programs that have to exist as files to be
    /// launched, so they cannot live inside a single-file executable the way the
    /// app's own assemblies do — they sit in a <c>tools</c> folder beside it
    /// instead. <see cref="AppContext.BaseDirectory"/> points at the host for a
    /// single-file build, but <see cref="Environment.ProcessPath"/> is checked
    /// too so a framework-dependent layout resolves the same way.
    /// </remarks>
    public static IEnumerable<string> BundledDirectories()
    {
        yield return Path.Combine(AppContext.BaseDirectory, "tools");

        string? processDirectory = Path.GetDirectoryName(Environment.ProcessPath ?? "");
        if (!string.IsNullOrEmpty(processDirectory))
        {
            yield return Path.Combine(processDirectory, "tools");
        }
    }

    /// <summary>Finds a tool, or returns null when it is not installed anywhere we look.</summary>
    public string? Find(ToolKind kind)
    {
        // 1. Explicit path from settings (ffprobe rides along next to ffmpeg).
        string configured = kind == ToolKind.YtDlp ? _ytDlpSetting() : _ffmpegSetting();
        if (!string.IsNullOrWhiteSpace(configured))
        {
            if (kind == ToolKind.Ffprobe)
            {
                string? directory = Path.GetDirectoryName(configured);
                if (directory is not null)
                {
                    string sibling = Path.Combine(directory, ExecutableName(ToolKind.Ffprobe));
                    if (File.Exists(sibling))
                    {
                        return sibling;
                    }
                }
            }
            else if (File.Exists(configured))
            {
                return configured;
            }
        }

        // 2. Shipped alongside the app, if this build bundles them.
        foreach (string directory in BundledDirectories())
        {
            string shipped = Path.Combine(directory, ExecutableName(kind));
            if (File.Exists(shipped))
            {
                return shipped;
            }
        }

        // 3. The config tools dir, where downloads land.
        string downloaded = Path.Combine(_toolsDirectory, ExecutableName(kind));
        if (File.Exists(downloaded))
        {
            return downloaded;
        }

        // 3. PATH lookup.
        return FindOnPath(ExecutableName(kind));
    }

    public static string? FindOnPath(string executableName)
    {
        string? pathVariable = Environment.GetEnvironmentVariable("PATH");
        if (pathVariable is null)
        {
            return null;
        }
        foreach (string directory in pathVariable.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                string candidate = Path.Combine(directory.Trim(), executableName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            catch (ArgumentException)
            {
                // Malformed PATH entry — skip.
            }
        }
        return null;
    }

    /// <summary>
    /// Downloads a missing tool into the tools dir, reporting 0–1 progress.
    /// Returns the installed executable path.
    /// </summary>
    public async Task<string> DownloadAsync(ToolKind kind, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        Directory.CreateDirectory(_toolsDirectory);

        if (kind == ToolKind.YtDlp)
        {
            string target = Path.Combine(_toolsDirectory, ExecutableName(ToolKind.YtDlp));
            await DownloadFileAsync(YtDlpDownloadUrl, target, progress, ct).ConfigureAwait(false);
            return target;
        }

        // ffmpeg/ffprobe come from the same zip; extract both binaries.
        string zipPath = Path.Combine(_toolsDirectory, "ffmpeg-download.zip");
        await DownloadFileAsync(FfmpegDownloadUrl, zipPath, progress, ct).ConfigureAwait(false);
        try
        {
            using var archive = ZipFile.OpenRead(zipPath);
            ExtractBinary(archive, "bin/ffmpeg.exe", Path.Combine(_toolsDirectory, "ffmpeg.exe"));
            ExtractBinary(archive, "bin/ffprobe.exe", Path.Combine(_toolsDirectory, "ffprobe.exe"));
        }
        finally
        {
            try
            {
                File.Delete(zipPath);
            }
            catch (IOException)
            {
                // Leftover zip is harmless.
            }
        }

        return Path.Combine(_toolsDirectory, ExecutableName(kind));
    }

    private static void ExtractBinary(ZipArchive archive, string entrySuffix, string target)
    {
        var entry = archive.Entries.FirstOrDefault(e =>
            e.FullName.Replace('\\', '/').EndsWith(entrySuffix, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            throw new InvalidOperationException($"The downloaded archive is missing {entrySuffix}.");
        }
        entry.ExtractToFile(target, overwrite: true);
    }

    private async Task DownloadFileAsync(string url, string target, IProgress<double>? progress, CancellationToken ct)
    {
        using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        long? total = response.Content.Headers.ContentLength;
        await using var source = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await using var destination = File.Create(target);

        var buffer = new byte[81920];
        long readTotal = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
        {
            await destination.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
            readTotal += read;
            if (total is > 0)
            {
                progress?.Report((double)readTotal / total.Value);
            }
        }
    }
}
