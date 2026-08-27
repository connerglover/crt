using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CRT.Core.Files;
using CRT.Core.Formatting;
using CRT.Core.Models;
using CRT.Core.Net;
using CRT.Services;

namespace CRT.ViewModels;

/// <summary>Row model for the "Runs to Verify" table.</summary>
public sealed partial class PendingRunViewModel : ObservableObject
{
    private readonly DashboardViewModel _parent;

    public PendingRunViewModel(DashboardViewModel parent, SrcPendingRun run)
    {
        _parent = parent;
        Run = run;
    }

    public SrcPendingRun Run { get; }

    public string GameName => Run.GameName;

    public string CategoryText => Run.Level is null ? Run.Category : $"{Run.Category} · {Run.Level}";

    public string Players => Run.Players;

    public string SubmittedText => Run.Submitted?.ToLocalTime().ToString("yyyy-MM-dd") ?? "";

    public string ClaimedTime => TimeFormatter.FormatIso(Math.Round(Run.PrimarySeconds, 3));

    public bool HasVideo => !string.IsNullOrEmpty(Run.VideoUrl);

    public string WatchLabel => AppServices.Loc["Watch"];

    public string RetimeLabel => AppServices.Loc["Retime This"];

    public string VerifyLabel => AppServices.Loc["Verify"];

    public string RejectLabel => AppServices.Loc["Reject"];

    [RelayCommand]
    private void Watch() => _parent.WatchRun(this);

    [RelayCommand]
    private Task RetimeAsync() => _parent.RetimeRunAsync(this);

    [RelayCommand]
    private Task VerifyAsync() => _parent.VerifyRunAsync(this);

    [RelayCommand]
    private Task RejectAsync() => _parent.RejectRunAsync(this);
}

/// <summary>Row model for "My Recent Runs".</summary>
public sealed class RecentRunViewModel
{
    public RecentRunViewModel(SrcRecentRun run)
    {
        Run = run;
    }

    public SrcRecentRun Run { get; }

    public string GameName => Run.GameName;

    public string Category => Run.Category;

    public string Time => TimeFormatter.FormatIso(Math.Round(Run.PrimarySeconds, 3));

    public string StatusGlyph => Run.Status switch
    {
        "verified" => "✓",
        "rejected" => "✕",
        _ => "⏳",
    };

    public string GameAndCategory =>
        string.IsNullOrEmpty(Run.Category) ? Run.GameName : $"{Run.GameName} — {Run.Category}";

    public string DateText => Run.Date ?? "";
}

/// <summary>Row model for the run library list.</summary>
public sealed partial class LibraryEntryViewModel : ObservableObject
{
    private readonly DashboardViewModel _parent;

    public LibraryEntryViewModel(DashboardViewModel parent, RunLibraryEntry entry)
    {
        _parent = parent;
        Entry = entry;
    }

    public RunLibraryEntry Entry { get; }

    public string Title => Entry.DisplayTitle;

    public string Game => Entry.Game;

    public string PrimaryTime => string.IsNullOrEmpty(Entry.TimeWithoutLoads) ? "00.000" : Entry.TimeWithoutLoads;

    public string SecondaryTime => Entry.TimeWithLoads;

    public string ModeChip => Entry.Mode == "segments"
        ? AppServices.Loc["Segments"]
        : AppServices.Loc["Loads"];

    public string ModifiedText
    {
        get
        {
            if (DateTimeOffset.TryParse(Entry.Modified, out DateTimeOffset modified))
            {
                return modified.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
            }
            return Entry.Modified;
        }
    }

    public string OpenLabel => AppServices.Loc["Open"];

    public string RevealLabel => AppServices.Loc["Reveal in Explorer"];

    public string CopyModNoteLabel => AppServices.Loc["Copy Mod Note"];

    public string RemoveLabel => AppServices.Loc["Remove from Library"];

    [RelayCommand]
    private Task OpenAsync() => _parent.OpenLibraryEntryAsync(this);

    [RelayCommand]
    private Task RevealAsync() => _parent.RevealEntryAsync(this);

    [RelayCommand]
    private void Remove() => _parent.RemoveEntry(this);

    [RelayCommand]
    private Task CopyModNoteAsync() => _parent.CopyEntryModNoteAsync(this);
}

/// <summary>
/// The dashboard: run library, quick actions, and the Speedrun.com moderation
/// panel (sign-in, runs to verify, my recent runs, 5-minute auto refresh).
/// </summary>
public sealed partial class DashboardViewModel : ObservableObject
{
    private SrcProfile? _profile;
    private string? _retimingRunId;
    private CancellationTokenSource? _refreshCts;

    public ObservableCollection<LibraryEntryViewModel> LibraryEntries { get; } = new();

    public ObservableCollection<PendingRunViewModel> PendingRuns { get; } = new();

    public ObservableCollection<RecentRunViewModel> RecentRuns { get; } = new();

    [ObservableProperty]
    private bool _isSignedIn;

    [ObservableProperty]
    private string _userName = "";

    [ObservableProperty]
    private string? _avatarUri;

    [ObservableProperty]
    private string _apiKeyInput = "";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _errorText = "";

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string _pendingHeader = "";

    [ObservableProperty]
    private bool _hasPendingRuns;

    [ObservableProperty]
    private bool _hasUnsavedSession;

    [ObservableProperty]
    private string _unsavedSessionText = "";

    [ObservableProperty]
    private bool _hasLibraryEntries;

    /// <summary>The run id currently being retimed via "Retime this", if any.</summary>
    public string? RetimingRunId => _retimingRunId;

    // ── Lifecycle ──────────────────────────────────────────────────────────

    /// <summary>Called when the dashboard becomes visible.</summary>
    public async Task ActivateAsync()
    {
        RefreshLibrary();
        RefreshUnsavedTile();

        if (!IsSignedIn && AppServices.ApiKeyStore.HasKey)
        {
            string? key = AppServices.ApiKeyStore.TryLoad();
            if (key is not null)
            {
                await TrySignInAsync(key, persist: false);
            }
        }

        StartAutoRefresh();
    }

    /// <summary>Called when the dashboard is navigated away from.</summary>
    public void Deactivate() => StopAutoRefresh();

    private void StartAutoRefresh()
    {
        StopAutoRefresh();
        var cts = new CancellationTokenSource();
        _refreshCts = cts;
        _ = AutoRefreshLoopAsync(cts.Token);
    }

    private void StopAutoRefresh()
    {
        _refreshCts?.Cancel();
        _refreshCts = null;
    }

    private async Task AutoRefreshLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMinutes(5), ct);
                if (IsSignedIn)
                {
                    await RefreshAsync();
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    public void RefreshLibrary()
    {
        // Spec §11.1: the library shows everything in recent.json plus any file
        // saved/opened by the app. Fold recent files the index doesn't know yet
        // (e.g. from an older install) into library.json.
        var known = new HashSet<string>(
            AppServices.Library.Entries.Select(e => e.Path), StringComparer.OrdinalIgnoreCase);
        foreach (string path in AppServices.RecentFiles.Paths)
        {
            if (known.Contains(path) || !File.Exists(path))
            {
                continue;
            }
            try
            {
                AppServices.Library.Upsert(RunFileStore.Load(path), path);
            }
            catch (Exception e) when (e is ValidationException or IOException or UnauthorizedAccessException)
            {
                // Unreadable recent file — skip it.
            }
        }

        LibraryEntries.Clear();
        foreach (var entry in AppServices.Library.Entries)
        {
            LibraryEntries.Add(new LibraryEntryViewModel(this, entry));
        }
        HasLibraryEntries = LibraryEntries.Count > 0;
    }

    public void RefreshUnsavedTile()
    {
        HasUnsavedSession = AppServices.Session.Dirty;
        UnsavedSessionText = HasUnsavedSession
            ? $"{AppServices.Loc["Unsaved Session"]} · {AppServices.Session.PrimaryTimeText}"
            : "";
    }

    // ── Quick actions ──────────────────────────────────────────────────────

    [RelayCommand]
    private async Task NewRetimeAsync()
    {
        await AppServices.Session.NewTimeAsync();
        AppServices.MainWindow?.NavigateTo("retimer");
    }

    [RelayCommand]
    private async Task OpenFileAsync()
    {
        await AppServices.Session.OpenTimeAsync();
        if (AppServices.Session.FilePath is not null)
        {
            AppServices.MainWindow?.NavigateTo("retimer");
        }
    }

    [RelayCommand]
    private void ImportVideo() => AppServices.MainWindow?.NavigateTo("video");

    [RelayCommand]
    private void ContinueEditing() => AppServices.MainWindow?.NavigateTo("retimer");

    // ── Library row actions ────────────────────────────────────────────────

    public async Task OpenLibraryEntryAsync(LibraryEntryViewModel entry)
    {
        if (!File.Exists(entry.Entry.Path))
        {
            // The file was moved or deleted — that is not a corrupt file, and
            // library.json keeps stale rows on purpose, so offer to drop this one.
            bool remove = await AppServices.Dialogs.ConfirmAsync(
                AppServices.Loc["Error"],
                AppServices.Loc.Format("File not found", ("path", entry.Entry.Path)),
                AppServices.Loc["Remove from Library"],
                AppServices.Loc["Cancel"]);
            if (remove)
            {
                RemoveEntry(entry);
            }
            return;
        }
        await AppServices.Session.OpenPathAsync(entry.Entry.Path);
        if (AppServices.Session.FilePath == entry.Entry.Path)
        {
            AppServices.MainWindow?.NavigateTo("retimer");
        }
    }

    public async Task RevealEntryAsync(LibraryEntryViewModel entry)
    {
        try
        {
            Process.Start("explorer.exe", $"/select,\"{entry.Entry.Path}\"");
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            await AppServices.Dialogs.ShowErrorAsync(e.Message);
        }
    }

    public void RemoveEntry(LibraryEntryViewModel entry)
    {
        AppServices.Library.Remove(entry.Entry.Path);
        RefreshLibrary();
    }

    public async Task CopyEntryModNoteAsync(LibraryEntryViewModel entry)
    {
        try
        {
            var session = RunFileStore.Load(entry.Entry.Path);
            ClipboardService.SetText(ModNoteBuilder.Build(session, AppServices.Settings.ModNoteFormat));
            AppServices.MainWindow?.ShowToast(AppServices.Loc["Mod note copied"]);
        }
        catch (Exception e) when (e is ValidationException or IOException or UnauthorizedAccessException)
        {
            await AppServices.Dialogs.ShowErrorAsync(e.Message);
        }
    }

    // ── Speedrun.com sign-in ───────────────────────────────────────────────

    [RelayCommand]
    private async Task SignInAsync()
    {
        string key = ApiKeyInput.Trim();
        if (key.Length == 0)
        {
            return;
        }
        await TrySignInAsync(key, persist: true);
    }

    private async Task TrySignInAsync(string apiKey, bool persist)
    {
        IsLoading = true;
        HasError = false;
        try
        {
            var profile = await AppServices.Speedrun.GetProfileAsync(apiKey);
            _profile = profile;
            AppServices.Speedrun.ApiKey = apiKey;
            if (persist)
            {
                AppServices.ApiKeyStore.Save(apiKey);
            }
            IsSignedIn = true;
            UserName = profile.Name;
            AvatarUri = profile.AvatarUri;
            ApiKeyInput = "";
            await RefreshAsync();
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException or KeyNotFoundException or InvalidOperationException)
        {
            HasError = true;
            ErrorText = AppServices.Loc["Sign-in failed"];
            if (!persist && e is HttpRequestException
                {
                    StatusCode: System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden
                })
            {
                // The stored key was rejected (not just a network blip) — forget it.
                AppServices.ApiKeyStore.Delete();
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void SignOut()
    {
        AppServices.ApiKeyStore.Delete();
        AppServices.Speedrun.ApiKey = null;
        _profile = null;
        IsSignedIn = false;
        UserName = "";
        AvatarUri = null;
        PendingRuns.Clear();
        RecentRuns.Clear();
        HasPendingRuns = false;
        PendingHeader = "";
        AppServices.MainWindow?.SetPendingRunsBadge(0);
    }

    [RelayCommand]
    private void OpenApiKeyPage()
    {
        try
        {
            Process.Start(new ProcessStartInfo(SpeedrunClient.ApiKeySettingsUrl) { UseShellExecute = true });
        }
        catch (Exception)
        {
            // Browser launch failure is not actionable.
        }
    }

    // ── Refresh ────────────────────────────────────────────────────────────

    [RelayCommand]
    public async Task RefreshAsync()
    {
        if (_profile is null)
        {
            return;
        }

        IsLoading = true;
        HasError = false;
        try
        {
            var games = await AppServices.Speedrun.GetModeratedGamesAsync(_profile.Id);
            var pending = await AppServices.Speedrun.GetAllPendingRunsAsync(games);
            var recent = await AppServices.Speedrun.GetRecentRunsAsync(_profile.Id);

            PendingRuns.Clear();
            foreach (var run in pending)
            {
                PendingRuns.Add(new PendingRunViewModel(this, run));
            }
            HasPendingRuns = PendingRuns.Count > 0;
            PendingHeader = AppServices.Loc.Format("Runs to Verify (n)", ("count", PendingRuns.Count));
            AppServices.MainWindow?.SetPendingRunsBadge(PendingRuns.Count);

            RecentRuns.Clear();
            foreach (var run in recent)
            {
                RecentRuns.Add(new RecentRunViewModel(run));
            }
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException or KeyNotFoundException or InvalidOperationException)
        {
            HasError = true;
            ErrorText = AppServices.Loc["Network error"];
        }
        finally
        {
            IsLoading = false;
        }
    }

    // ── Runs to Verify row actions ─────────────────────────────────────────

    public void WatchRun(PendingRunViewModel run)
    {
        string? url = run.Run.VideoUrl ?? run.Run.WebLink;
        if (url is null)
        {
            return;
        }
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception)
        {
            // Browser launch failure is not actionable.
        }
    }

    public async Task RetimeRunAsync(PendingRunViewModel run)
    {
        _retimingRunId = run.Run.Id;
        string? video = run.Run.VideoUrl;
        if (video is not null && YtDlpImporterIsYouTube(video))
        {
            AppServices.MainWindow?.NavigateTo("video");
            await AppServices.VideoRetimer.ImportFromUrlAsync(video);
        }
        else
        {
            await AppServices.Session.NewTimeAsync();
            AppServices.MainWindow?.NavigateTo("retimer");
        }
    }

    private static bool YtDlpImporterIsYouTube(string url) => Core.Tools.YtDlpImporter.IsYouTubeUrl(url);

    public async Task VerifyRunAsync(PendingRunViewModel run)
    {
        string message = AppServices.Loc["Verify Run Message"];
        if (run.Run.Id == _retimingRunId)
        {
            message += "\n\n" + ModNoteBuilder.Build(AppServices.Session.Session, AppServices.Settings.ModNoteFormat);
        }
        bool confirmed = await AppServices.Dialogs.ConfirmAsync(
            AppServices.Loc["Verify"], message, AppServices.Loc["Verify"], AppServices.Loc["Cancel"]);
        if (!confirmed)
        {
            return;
        }
        await SetRunStatusAsync(run, verified: true, reason: null);
    }

    public async Task RejectRunAsync(PendingRunViewModel run)
    {
        string prefill = run.Run.Id == _retimingRunId
            ? ModNoteBuilder.Build(AppServices.Session.Session, AppServices.Settings.ModNoteFormat)
            : "";
        string? reason = await AppServices.Dialogs.PromptTextAsync(
            AppServices.Loc["Reject"], AppServices.Loc["Reject Run Message"], prefill, multiline: true);
        if (reason is null || reason.Trim().Length == 0)
        {
            return;
        }
        await SetRunStatusAsync(run, verified: false, reason: reason);
    }

    private async Task SetRunStatusAsync(PendingRunViewModel run, bool verified, string? reason)
    {
        try
        {
            await AppServices.Speedrun.SetRunStatusAsync(run.Run.Id, verified, reason);
            PendingRuns.Remove(run);
            HasPendingRuns = PendingRuns.Count > 0;
            PendingHeader = AppServices.Loc.Format("Runs to Verify (n)", ("count", PendingRuns.Count));
            AppServices.MainWindow?.SetPendingRunsBadge(PendingRuns.Count);
            if (run.Run.Id == _retimingRunId)
            {
                _retimingRunId = null;
            }
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            await AppServices.Dialogs.ShowErrorAsync(e.Message);
        }
    }
}
