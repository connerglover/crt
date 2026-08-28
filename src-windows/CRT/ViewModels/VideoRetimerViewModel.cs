using System.Diagnostics;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CRT.Core.Formatting;
using CRT.Core.Models;
using CRT.Core.Tools;
using CRT.Services;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace CRT.ViewModels;

/// <summary>
/// The video retimer workspace: import (local / direct URL / YouTube), a
/// frame-accurate player, mark keys writing into the shared session, and the
/// ffmpeg timer-overlay export.
/// </summary>
public sealed partial class VideoRetimerViewModel : ObservableObject
{
    private static readonly string[] VideoExtensions =
        { ".mp4", ".mkv", ".mov", ".webm", ".avi", ".m4v", ".ts" };

    public MediaPlayer Player { get; } = new() { AudioCategory = MediaPlayerAudioCategory.Movie };

    private VideoInfo? _videoInfo;
    private int? _pendingSegmentStart;
    private int? _pendingLoadStart;

    [ObservableProperty]
    private string _importUrl = "";

    [ObservableProperty]
    private string _statusText = "";

    [ObservableProperty]
    private bool _hasVideo;

    [ObservableProperty]
    private bool _isImporting;

    [ObservableProperty]
    private string _videoPath = "";

    [ObservableProperty]
    private string _videoInfoText = "";

    [ObservableProperty]
    private string _currentFrameText = "0";

    [ObservableProperty]
    private string _currentTimeText = "00.000";

    [ObservableProperty]
    private double _sliderMaxSeconds = 1;

    [ObservableProperty]
    private double _sliderSeconds;

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private string _pendingMarkText = "";

    /// <summary>Normal-play speed multiplier chosen from the speed picker.</summary>
    [ObservableProperty]
    private double _playbackRate = 1.0;

    /// <summary>
    /// Trick-play scan multiplier: 0 when off, positive fast-forward, negative
    /// rewind. Shown next to the transport so the state is never ambiguous.
    /// </summary>
    [ObservableProperty]
    private int _scanRate;

    [ObservableProperty]
    private string _scanText = "";

    private SessionViewModel Session => AppServices.Session;

    public decimal Fps => _videoInfo?.Fps is { } fps and > 0m ? fps : Session.Session.Framerate;

    public decimal DurationSeconds => _videoInfo?.DurationSeconds ?? 0m;

    /// <summary>Current playback position in seconds (decimal, from the player clock).</summary>
    public decimal PositionSeconds => (decimal)Player.PlaybackSession.Position.TotalSeconds;

    /// <summary>
    /// The frame the user is on. While stepping this is the frame we asked for
    /// rather than one re-derived from the clock: a decoder that presents a hair
    /// early or late would otherwise turn a run of single steps into a drifting
    /// counter.
    /// </summary>
    public int CurrentFrame => _frameCursor ?? FrameFromClock;

    private int FrameFromClock => Fps == 0m
        ? 0
        : (int)Math.Round(PositionSeconds * Fps, 0, MidpointRounding.ToEven);

    private int? _frameCursor;

    // ── Import ─────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task BrowseAsync()
    {
        var picker = new FileOpenPicker();
        foreach (string extension in VideoExtensions)
        {
            picker.FileTypeFilter.Add(extension);
        }
        if (AppServices.MainWindow is { } window)
        {
            WinRT.Interop.InitializeWithWindow.Initialize(
                picker, WinRT.Interop.WindowNative.GetWindowHandle(window));
        }
        var file = await picker.PickSingleFileAsync();
        if (file is not null)
        {
            await LoadVideoAsync(file.Path);
        }
    }

    [RelayCommand]
    private async Task ImportAsync()
    {
        string input = ImportUrl.Trim();
        if (input.Length == 0)
        {
            return;
        }

        if (File.Exists(input))
        {
            await LoadVideoAsync(input);
            return;
        }
        if (!input.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !input.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            await AppServices.Dialogs.ShowErrorAsync(AppServices.Loc["No video loaded"]);
            return;
        }

        IsImporting = true;
        try
        {
            string? localPath = YtDlpImporter.IsYouTubeUrl(input)
                ? await DownloadYouTubeAsync(input)
                : await DownloadDirectUrlAsync(input);
            if (localPath is not null)
            {
                await LoadVideoAsync(localPath);
            }
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            await AppServices.Dialogs.ShowErrorAsync(e.Message);
        }
        finally
        {
            IsImporting = false;
        }
    }

    /// <summary>Entry point for "Retime this" on the dashboard: prefill + import immediately.</summary>
    public async Task ImportFromUrlAsync(string url)
    {
        ImportUrl = url;
        await ImportAsync();
    }

    private async Task<string?> DownloadYouTubeAsync(string url)
    {
        string? ytDlp = await EnsureToolAsync(ToolKind.YtDlp);
        if (ytDlp is null)
        {
            return null;
        }

        var importer = new YtDlpImporter(ytDlp, AppServices.Paths.VideoCacheDirectory);
        string? path = null;
        bool completed = await AppServices.Dialogs.RunWithProgressAsync(
            AppServices.Loc["Downloading"],
            async (progress, ct) => path = await importer.DownloadAsync(url, progress, ct));
        return completed ? path : null;
    }

    private async Task<string?> DownloadDirectUrlAsync(string url)
    {
        Directory.CreateDirectory(AppServices.Paths.VideoCacheDirectory);
        string extension = Path.GetExtension(new Uri(url).AbsolutePath);
        if (!VideoExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            extension = ".mp4";
        }
        string target = Path.Combine(
            AppServices.Paths.VideoCacheDirectory,
            $"direct-{Convert.ToHexString(System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(url)))[..12]}{extension}");
        if (File.Exists(target))
        {
            return target;
        }

        string? result = null;
        bool completed = await AppServices.Dialogs.RunWithProgressAsync(
            AppServices.Loc["Downloading"],
            async (progress, ct) =>
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
                using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
                response.EnsureSuccessStatusCode();
                long? total = response.Content.Headers.ContentLength;
                await using var source = await response.Content.ReadAsStreamAsync(ct);
                await using var destination = File.Create(target);
                var buffer = new byte[81920];
                long readTotal = 0;
                int read;
                while ((read = await source.ReadAsync(buffer, ct)) > 0)
                {
                    await destination.WriteAsync(buffer.AsMemory(0, read), ct);
                    readTotal += read;
                    if (total is > 0)
                    {
                        progress.Report((double)readTotal / total.Value);
                    }
                }
                result = target;
            });
        if (!completed)
        {
            try
            {
                File.Delete(target);
            }
            catch (IOException)
            {
            }
        }
        return completed ? result : null;
    }

    // ── Session binding ────────────────────────────────────────────────────

    /// <summary>
    /// Unloads the current video. Called when the session is replaced by a new
    /// time: the marks are gone, so leaving the footage loaded would invite
    /// marking a fresh run against the previous video.
    /// </summary>
    public void CloseVideo()
    {
        StopScan();
        _reverseTimer?.Stop();
        Player.Pause();
        Player.Source = null;
        _videoInfo = null;
        HasVideo = false;
        VideoPath = "";
        ImportUrl = "";
        VideoInfoText = "";
        StatusText = "";
        CurrentFrameText = "0";
        CurrentTimeText = "00.000";
        SliderMaxSeconds = 1;
        SliderSeconds = 0;
        IsPlaying = false;
        _pendingLoadStart = null;
        _pendingSegmentStart = null;
        _frameCursor = null;
        PendingMarkText = "";
    }

    /// <summary>
    /// Reopens the video recorded in a run that has just been loaded, if it is
    /// still where it was. A missing file is reported in the status line rather
    /// than a dialog — the run itself opened fine, and the times are readable
    /// without the footage.
    /// </summary>
    public async Task RestoreVideoForSessionAsync()
    {
        string path = Session.Session.Meta.VideoPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            CloseVideo();
            return;
        }
        if (string.Equals(path, VideoPath, StringComparison.OrdinalIgnoreCase) && HasVideo)
        {
            return; // already open
        }

        CloseVideo();
        if (!File.Exists(path))
        {
            StatusText = $"{AppServices.Loc["Video Missing"]} {Path.GetFileName(path)}";
            return;
        }

        await LoadVideoAsync(path);
        if (HasVideo)
        {
            AppServices.MainWindow?.ShowToast(AppServices.Loc["Video Reopened"]);
        }
    }

    private async Task LoadVideoAsync(string path)
    {
        StatusText = AppServices.Loc["Probing"];
        try
        {
            string? ffprobe = await EnsureToolAsync(ToolKind.Ffprobe);
            if (ffprobe is null)
            {
                // Spec §9.1 makes the probe part of importing, and the timeline,
                // the seek clamp and the export all read the probed duration and
                // resolution — so a missing ffprobe fails the import outright
                // instead of loading a half-working player.
                StatusText = HasVideo ? Path.GetFileName(VideoPath) : "";
                await AppServices.Dialogs.ShowErrorAsync(AppServices.Loc["ffprobe Required"]);
                return;
            }

            var client = new FfprobeClient(ffprobe);
            _videoInfo = await client.ProbeAsync(path);

            if (_videoInfo.Fps > 0m && _videoInfo.Fps != Session.Session.Framerate)
            {
                Session.SetFramerate(_videoInfo.Fps);
                AppServices.MainWindow?.ShowToast(
                    AppServices.Loc.Format("Framerate set from video", ("fps", _videoInfo.Fps)));
            }
            VideoInfoText =
                $"{_videoInfo.Width}×{_videoInfo.Height} · {_videoInfo.Fps} fps ({_videoInfo.FpsRational}) · " +
                TimeFormatter.FormatIso(Math.Round(_videoInfo.DurationSeconds, 3));
            SliderMaxSeconds = Math.Max(1, (double)_videoInfo.DurationSeconds);

            var storageFile = await StorageFile.GetFileFromPathAsync(path);
            Player.Source = MediaSource.CreateFromStorageFile(storageFile);
            Player.Pause();

            VideoPath = path;
            HasVideo = true;
            StatusText = Path.GetFileName(path);
            _pendingLoadStart = null;
            _pendingSegmentStart = null;
            PendingMarkText = "";
            UpdatePosition();

            // The video belongs to the run, so saving records it and reopening
            // the run brings it back.
            Session.Session.Meta.VideoPath = path;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            await AppServices.Dialogs.ShowErrorAsync(e.Message);
            StatusText = "";
        }
    }

    // ── Tool acquisition (spec §8 step 4) ──────────────────────────────────

    public async Task<string?> EnsureToolAsync(ToolKind kind)
    {
        string? found = AppServices.Tools.Find(kind);
        if (found is not null)
        {
            return found;
        }

        bool download = await AppServices.Dialogs.ConfirmAsync(
            ToolLocator.DisplayName(kind),
            AppServices.Loc.Format("Tool Needed",
                ("tool", ToolLocator.DisplayName(kind)),
                ("size", ToolLocator.ApproxDownloadSize(kind))),
            AppServices.Loc["Download"], AppServices.Loc["Cancel"]);
        if (!download)
        {
            return null;
        }

        string? path = null;
        bool completed = await AppServices.Dialogs.RunWithProgressAsync(
            AppServices.Loc["Downloading"],
            async (progress, ct) => path = await AppServices.Tools.DownloadAsync(kind, progress, ct));
        return completed ? path : null;
    }

    // ── Playback / stepping ────────────────────────────────────────────────

    [RelayCommand]
    public void PlayPause()
    {
        if (!HasVideo)
        {
            return;
        }
        StopScan();
        if (Player.PlaybackSession.PlaybackState == MediaPlaybackState.Playing)
        {
            Player.Pause();
        }
        else
        {
            Player.PlaybackSession.PlaybackRate = PlaybackRate;
            Player.Play();
        }
        UpdatePosition();
    }

    // ── Playback speed + trick play ────────────────────────────────────────

    /// <summary>Speed multipliers offered by the picker.</summary>
    public static IReadOnlyList<double> PlaybackRates { get; } =
        new[] { 0.1, 0.25, 0.5, 1.0, 1.25, 1.5, 2.0, 4.0 };

    /// <summary>
    /// Sets the normal-play speed. Slow motion is the useful direction here:
    /// finding the exact frame a load ends is far easier at 0.1x than by
    /// stepping, and the frame counter stays accurate at any rate because it is
    /// derived from the clock rather than counted.
    /// </summary>
    public void SetPlaybackRate(double rate)
    {
        PlaybackRate = rate;
        StopScan();
        if (HasVideo)
        {
            Player.PlaybackSession.PlaybackRate = rate;
        }
    }

    /// <summary>
    /// Fast-forward. Repeated presses climb 2x, 4x, 8x, 16x then wrap, the way a
    /// transport control is expected to behave; pressing the opposite direction
    /// reverses instead of continuing to climb.
    /// </summary>
    [RelayCommand]
    public void ScanForward() => Scan(ScanRate >= 0 ? NextScanStep(ScanRate) : 2);

    /// <summary>Rewind, mirroring <see cref="ScanForward"/>.</summary>
    [RelayCommand]
    public void ScanBackward() => Scan(ScanRate <= 0 ? -NextScanStep(-ScanRate) : -2);

    private static int NextScanStep(int current) => current switch
    {
        0 or 1 => 2,
        2 => 4,
        4 => 8,
        8 => 16,
        _ => 2,
    };

    private void Scan(int rate)
    {
        if (!HasVideo)
        {
            return;
        }

        ScanRate = rate;
        ScanText = rate == 0 ? "" : (rate > 0 ? $"\u25b6\u25b6 {rate}x" : $"\u25c0\u25c0 {-rate}x");

        if (rate > 0)
        {
            // Forward scan is real playback at speed, so audio/decode stay the
            // player's problem.
            _reverseTimer?.Stop();
            Player.PlaybackSession.PlaybackRate = rate;
            Player.Play();
            return;
        }

        // MediaPlayer has no negative playback rate, so rewind is stepping the
        // position backwards on a timer while the pipeline stays paused.
        Player.Pause();
        Player.PlaybackSession.PlaybackRate = PlaybackRate;
        _reverseTimer ??= CreateReverseTimer();
        _reverseTimer.Start();
    }

    /// <summary>Returns to normal speed. Any transport action cancels a scan.</summary>
    public void StopScan()
    {
        if (ScanRate == 0)
        {
            return;
        }
        ScanRate = 0;
        ScanText = "";
        _reverseTimer?.Stop();
        if (HasVideo)
        {
            Player.PlaybackSession.PlaybackRate = PlaybackRate;
        }
    }

    private Microsoft.UI.Dispatching.DispatcherQueueTimer CreateReverseTimer()
    {
        var timer = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread().CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(ReverseTickMs);
        timer.Tick += (_, _) =>
        {
            if (ScanRate >= 0)
            {
                timer.Stop();
                return;
            }
            decimal step = (decimal)(-ScanRate * ReverseTickMs) / 1000m;
            decimal target = PositionSeconds - step;
            if (target <= 0m)
            {
                SeekSeconds(0m);
                StopScan();
                return;
            }
            SeekSeconds(target);
        };
        return timer;
    }

    private const int ReverseTickMs = 100;
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _reverseTimer;

    [RelayCommand]
    public void StepForward() => StepFrames(1);

    /// <summary>
    /// Moves by whole frames.
    /// </summary>
    /// <remarks>
    /// This replaces MediaPlayer's StepForwardOneFrame/StepBackwardOneFrame,
    /// which were neither accurate nor quick: stepping back routinely moved
    /// about three frames and took roughly a second to present, because the API
    /// resolves backwards steps through the surrounding keyframe. Seeking to a
    /// computed frame time is exact and immediate.
    /// </remarks>
    public void StepFrames(int delta)
    {
        if (!HasVideo || Fps == 0m)
        {
            return;
        }
        StopScan();
        Player.Pause();

        int target = Math.Max(0, CurrentFrame + delta);
        if (DurationSeconds > 0m)
        {
            int lastFrame = (int)Math.Floor(DurationSeconds * Fps) - 1;
            if (lastFrame >= 0)
            {
                target = Math.Min(target, lastFrame);
            }
        }

        _frameCursor = target;
        // Land a quarter into the frame: seeking to its exact boundary can
        // resolve to either neighbour depending on how the demuxer rounds.
        Player.PlaybackSession.Position =
            TimeSpan.FromSeconds((double)((target + 0.25m) / Fps));
        UpdatePosition();
    }

    [RelayCommand]
    public void StepBackward() => StepFrames(-1);

    /// <summary>Arrow keys: ±5 frames; Shift+Arrow: ±1 second.</summary>
    public void JumpFrames(int frames) => StepFrames(frames);

    public void JumpSeconds(decimal seconds)
    {
        if (!HasVideo)
        {
            return;
        }
        Player.Pause();
        SeekSeconds(PositionSeconds + seconds);
    }

    public void SeekSeconds(decimal seconds)
    {
        _frameCursor = null;
        decimal clamped = Math.Max(0m, DurationSeconds > 0m ? Math.Min(seconds, DurationSeconds) : seconds);
        Player.PlaybackSession.Position = TimeSpan.FromSeconds((double)clamped);
        UpdatePosition();
    }

    /// <summary>Called by the page's UI timer and after every transport action.</summary>
    public void UpdatePosition()
    {
        IsPlaying = Player.PlaybackSession.PlaybackState == MediaPlaybackState.Playing;
        if (IsPlaying)
        {
            _frameCursor = null;
        }
        int frame = CurrentFrame;
        CurrentFrameText = frame.ToString(CultureInfo.InvariantCulture);
        CurrentTimeText = TimeFormatter.FormatFrameTime(frame, Fps, TimeSession.DefaultPrecision);
        SliderSeconds = (double)PositionSeconds;
    }

    // ── Marking ────────────────────────────────────────────────────────────

    /// <summary>"[": run start (loads mode) or segment start (segment mode).</summary>
    [RelayCommand]
    public async Task MarkStartAsync()
    {
        if (!HasVideo)
        {
            return;
        }
        int frame = CurrentFrame;
        if (Session.Session.Mode == TimingMode.Segments)
        {
            _pendingSegmentStart = frame;
            PendingMarkText = $"{AppServices.Loc["Segment Start"]}: {frame}";
        }
        else
        {
            Session.SetStartFrame(frame);
        }
        await Task.CompletedTask;
    }

    /// <summary>"]": run end (loads mode) or completes the pending segment (segment mode).</summary>
    [RelayCommand]
    public async Task MarkEndAsync()
    {
        if (!HasVideo)
        {
            return;
        }
        int frame = CurrentFrame;
        if (Session.Session.Mode == TimingMode.Segments)
        {
            if (_pendingSegmentStart is int start)
            {
                _pendingSegmentStart = null;
                PendingMarkText = "";
                await Session.AddRangeDirectAsync(start, frame);
            }
            else
            {
                PendingMarkText = AppServices.Loc["Mark Segment Start"];
            }
        }
        else
        {
            Session.SetEndFrame(frame);
        }
    }

    /// <summary>"L": marks the start of a load (loads mode only).</summary>
    [RelayCommand]
    public async Task MarkLoadStartAsync()
    {
        if (!HasVideo || Session.Session.Mode != TimingMode.Loads)
        {
            return;
        }
        _pendingLoadStart = CurrentFrame;
        PendingMarkText = $"{AppServices.Loc["Mark Load Start"]}: {_pendingLoadStart}";
        await Task.CompletedTask;
    }

    /// <summary>"Shift+L": completes the pending load.</summary>
    [RelayCommand]
    public async Task MarkLoadEndAsync()
    {
        if (!HasVideo || Session.Session.Mode != TimingMode.Loads)
        {
            return;
        }
        if (_pendingLoadStart is int start)
        {
            _pendingLoadStart = null;
            PendingMarkText = "";
            await Session.AddRangeDirectAsync(start, CurrentFrame);
        }
        else
        {
            PendingMarkText = AppServices.Loc["Mark Load Start"];
        }
    }

    // ── Export (spec §9.3) ─────────────────────────────────────────────────

    [RelayCommand]
    public async Task ExportAsync()
    {
        if (!HasVideo || _videoInfo is null)
        {
            await AppServices.Dialogs.ShowErrorAsync(AppServices.Loc["No video loaded"]);
            return;
        }

        var session = Session.Session;
        decimal fps = Fps;
        if (fps == 0m)
        {
            await AppServices.Dialogs.ShowErrorAsync(AppServices.Loc["No video loaded"]);
            return;
        }

        int startFrame = session.EffectiveStartFrame;
        int endFrame = session.EffectiveEndFrame;
        if (endFrame <= startFrame)
        {
            await AppServices.Dialogs.ShowErrorAsync(AppServices.Loc["Run End Before Start"]);
            return;
        }

        string? ffmpeg = await EnsureToolAsync(ToolKind.Ffmpeg);
        if (ffmpeg is null)
        {
            return;
        }

        var savePicker = new FileSavePicker
        {
            SuggestedFileName = Path.GetFileNameWithoutExtension(VideoPath) + "-retimed",
        };
        savePicker.FileTypeChoices.Add("MP4 Video", new List<string> { ".mp4" });
        if (AppServices.MainWindow is { } window)
        {
            WinRT.Interop.InitializeWithWindow.Initialize(
                savePicker, WinRT.Interop.WindowNative.GetWindowHandle(window));
        }
        var outputFile = await savePicker.PickSaveFileAsync();
        if (outputFile is null)
        {
            return;
        }

        decimal runStart = startFrame / fps;
        decimal runEnd = endFrame / fps;
        var pauses = BuildPauses(session, fps);
        var (trimStart, trimEnd) = FfmpegExporter.ComputeTrim(runStart, runEnd, _videoInfo.DurationSeconds);

        var settings = AppServices.Settings;
        var options = new TimerOverlayOptions(
            VideoHeight: _videoInfo.Height > 0 ? _videoInfo.Height : 1080)
        {
            Format = settings.TimerFormat,
            ClockStyle = TimerClockStyleExtensions.ParseSerialString(settings.TimerClockStyle),
            Corner = settings.TimerCorner,
            FontFamily = settings.TimerFontFamily,
            Bold = settings.TimerBold,
            TextSizePercent = settings.TimerTextSize,
            TextColor = settings.TimerTextColor,
            Background = settings.TimerBackground,
            BackgroundColor = settings.TimerBackgroundColor,
            BackgroundOpacity = settings.TimerBackgroundOpacity,
        };
        string chain = TimerFiltergraphBuilder.Build(runStart, runEnd, pauses, trimStart, options);

        var exporter = new FfmpegExporter(ffmpeg);
        string outputPath = outputFile.Path;
        bool completed;
        try
        {
            completed = await AppServices.Dialogs.RunWithProgressAsync(
                AppServices.Loc["Exporting"],
                (progress, ct) => exporter.ExportAsync(VideoPath, outputPath, trimStart, trimEnd, chain, progress, ct));
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            await AppServices.Dialogs.ShowErrorAsync(e.Message);
            return;
        }

        if (completed)
        {
            await ShowExportSuccessAsync(outputPath);
        }
    }

    private static List<TimerFiltergraphBuilder.Pause> BuildPauses(TimeSession session, decimal fps)
    {
        if (session.Mode == TimingMode.Segments)
        {
            var (_, _, gaps) = Core.Files.SegmentMath.ToRunBoundsAndGaps(session.Segments);
            return gaps.Select(g => new TimerFiltergraphBuilder.Pause(g.StartFrame / fps, g.EndFrame / fps)).ToList();
        }
        return session.Loads
            .OrderBy(l => l.StartFrame)
            .Select(l => new TimerFiltergraphBuilder.Pause(l.StartFrame / fps, l.EndFrame / fps))
            .ToList();
    }

    private async Task ShowExportSuccessAsync(string outputPath)
    {
        // Three buttons rather than a confirm, so dismissing the dialog (Escape)
        // neither opens the file nor the folder.
        var choice = await AppServices.Dialogs.ChooseAsync(
            AppServices.Loc["Export Complete"],
            outputPath,
            AppServices.Loc["Open"],
            AppServices.Loc["Show in Folder"],
            AppServices.Loc["Cancel"]);
        if (choice == DialogChoice.Dismissed)
        {
            return;
        }
        try
        {
            if (choice == DialogChoice.Primary)
            {
                Process.Start(new ProcessStartInfo(outputPath) { UseShellExecute = true });
            }
            else
            {
                Process.Start("explorer.exe", $"/select,\"{outputPath}\"");
            }
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            await AppServices.Dialogs.ShowErrorAsync(e.Message);
        }
    }
}
