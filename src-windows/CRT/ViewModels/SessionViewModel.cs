using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CRT.Core.Files;
using CRT.Core.Formatting;
using CRT.Core.Models;
using CRT.Core.Parsing;
using CRT.Services;
using Windows.Storage.Pickers;

namespace CRT.ViewModels;

/// <summary>
/// The one shared run session: timing model + file state + undo/redo +
/// autosave + all copy/file actions. The frame retimer and video retimer are
/// two views over this single view model.
/// </summary>
public sealed partial class SessionViewModel : ObservableObject
{
    private readonly SessionFileManager _files = new();
    private readonly Stack<TimeSession> _undoStack = new();
    private readonly Stack<TimeSession> _redoStack = new();

    /// <summary>
    /// Undo depth of the last saved (or freshly loaded) state, so undoing back
    /// to it clears the dirty flag. -1 once the edit history has branched away
    /// from that state and the depth alone can no longer identify it.
    /// </summary>
    private int _savedUndoDepth;

    /// <summary>(videoId, itag) pairs already prompted about this app session.</summary>
    private readonly HashSet<(string, string)> _frameratePromptSeen = new();

    public SessionViewModel()
    {
        RefreshAll();
    }

    public TimeSession Session => _files.Session;

    public string? FilePath => _files.FilePath;

    public bool Dirty => _files.Dirty;

    public IReadOnlyList<string> History => _files.History();

    /// <summary>Raised after new/open/restore/mode switch so views resync inputs.</summary>
    public event EventHandler? SessionReloaded;

    /// <summary>Raised on every value change (displays, sidebar, dashboard tile).</summary>
    public event EventHandler? SessionChanged;

    // ── Bindable state ─────────────────────────────────────────────────────

    [ObservableProperty]
    private string _framerateText = "60";

    [ObservableProperty]
    private string _startFrameText = "0";

    [ObservableProperty]
    private string _endFrameText = "0";

    [ObservableProperty]
    private string _rangeStartText = "0";

    [ObservableProperty]
    private string _rangeEndText = "0";

    [ObservableProperty]
    private string _primaryTimeText = "00.000";

    [ObservableProperty]
    private string _secondaryTimeText = "00.000";

    [ObservableProperty]
    private string _primaryLabel = "";

    [ObservableProperty]
    private string _secondaryLabel = "";

    [ObservableProperty]
    private bool _isSegmentMode;

    [ObservableProperty]
    private string _rowsHeader = "";

    [ObservableProperty]
    private string _rowsEmptyText = "";

    [ObservableProperty]
    private bool _hasRows;

    [ObservableProperty]
    private bool _canClearRows;

    [ObservableProperty]
    private string _rangeStartLabel = "";

    [ObservableProperty]
    private string _rangeEndLabel = "";

    [ObservableProperty]
    private string _addRangeLabel = "";

    [ObservableProperty]
    private string _clearRowsLabel = "";

    [ObservableProperty]
    private string _windowSubtitle = "";

    [ObservableProperty]
    private bool _canUndo;

    [ObservableProperty]
    private bool _canRedo;

    public ObservableCollection<RangeRowViewModel> Rows { get; } = new();

    // ── Initialization ─────────────────────────────────────────────────────

    public void ApplyDefaultMode(string defaultMode)
    {
        Session.Mode = TimingModeExtensions.ParseSerialString(defaultMode);
        RefreshAll();
    }

    // ── Frame parsing (with §2.1 framerate mismatch check) ────────────────

    /// <summary>
    /// Parses a frame input against the session framerate, first offering to
    /// correct the framerate when pasted YouTube debug info reveals a mismatch.
    /// </summary>
    public async Task<int> ParseFrameAsync(string text)
    {
        if (FrameInputParser.IsDebugInfo(text))
        {
            await ConfirmFramerateFromDebugInfoAsync(text);
        }
        return FrameInputParser.ParseFrameInput(text, Session.Framerate);
    }

    private async Task ConfirmFramerateFromDebugInfoAsync(string debugInfo)
    {
        var ids = DebugInfo.ExtractIds(debugInfo);
        if (ids is null || _frameratePromptSeen.Contains(ids.Value))
        {
            return;
        }

        decimal? detected;
        AppServices.MainWindow?.SetBusy(true);
        try
        {
            detected = await AppServices.Innertube.GetFormatFramerateAsync(ids.Value.VideoId, ids.Value.FormatId);
        }
        finally
        {
            AppServices.MainWindow?.SetBusy(false);
        }

        if (detected is null)
        {
            return;
        }

        decimal current = Session.Framerate;
        if (Math.Abs(detected.Value - current) < 1m)
        {
            return;
        }

        _frameratePromptSeen.Add(ids.Value);
        bool update = await AppServices.Dialogs.ConfirmAsync(
            AppServices.Loc["Framerate Mismatch"],
            AppServices.Loc.Format("Framerate Mismatch Message",
                ("detected", detected.Value), ("current", current)),
            AppServices.Loc["OK"], AppServices.Loc["Cancel"]);
        if (update)
        {
            CommitFramerate(detected.Value.ToString(CultureInfo.InvariantCulture));
        }
    }

    // ── Input commits ──────────────────────────────────────────────────────

    public void CommitFramerate(string text)
    {
        decimal framerate = FrameInputParser.CleanFramerate(text);
        if (framerate == Session.Framerate)
        {
            FramerateText = framerate.ToString(CultureInfo.InvariantCulture);
            RepaintInput(nameof(FramerateText));
            return;
        }
        PushUndo();
        Session.Mutate(framerate: framerate);
        _files.Dirty = true;
        FramerateText = framerate.ToString(CultureInfo.InvariantCulture);
        RepaintInput(nameof(FramerateText));
        RefreshAll();
    }

    public async Task CommitStartFrameAsync(string text)
    {
        try
        {
            int frame = await ParseFrameAsync(text);
            if (frame != Session.StartFrame)
            {
                PushUndo();
                Session.Mutate(startFrame: frame);
                _files.Dirty = true;
            }
            StartFrameText = frame.ToString();
            RepaintInput(nameof(StartFrameText));
            RefreshAll();
        }
        catch (ValidationException e)
        {
            await AppServices.Dialogs.ShowErrorAsync(e.Message);
        }
    }

    public async Task CommitEndFrameAsync(string text)
    {
        try
        {
            int frame = await ParseFrameAsync(text);
            if (frame != Session.EndFrame)
            {
                PushUndo();
                Session.Mutate(endFrame: frame);
                _files.Dirty = true;
            }
            EndFrameText = frame.ToString();
            RepaintInput(nameof(EndFrameText));
            RefreshAll();
        }
        catch (ValidationException e)
        {
            await AppServices.Dialogs.ShowErrorAsync(e.Message);
        }
    }

    /// <summary>Cleans a loads/segment field into a frame number (no session mutation).</summary>
    public async Task CommitRangeFieldAsync(bool isStart, string text)
    {
        int frame = 0;
        try
        {
            frame = await ParseFrameAsync(text);
        }
        catch (ValidationException e)
        {
            await AppServices.Dialogs.ShowErrorAsync(e.Message);
        }
        if (isStart)
        {
            RangeStartText = frame.ToString();
            RepaintInput(nameof(RangeStartText));
        }
        else
        {
            RangeEndText = frame.ToString();
            RepaintInput(nameof(RangeEndText));
        }
    }

    /// <summary>
    /// Re-announces an input property so its one-way bound TextBox is repainted
    /// even when the parsed value equals the one the view model already held
    /// (spec §6: the committed value always replaces the field text). The
    /// generated <c>[ObservableProperty]</c> setters skip the notification in
    /// that case, which would otherwise leave the raw text on screen.
    /// </summary>
    private void RepaintInput(string propertyName) => OnPropertyChanged(propertyName);

    public async Task PasteStartFrameAsync() =>
        await CommitStartFrameAsync(await ClipboardService.GetTextAsync());

    public async Task PasteEndFrameAsync() =>
        await CommitEndFrameAsync(await ClipboardService.GetTextAsync());

    public async Task PasteRangeStartAsync() =>
        await CommitRangeFieldAsync(true, await ClipboardService.GetTextAsync());

    public async Task PasteRangeEndAsync() =>
        await CommitRangeFieldAsync(false, await ClipboardService.GetTextAsync());

    // ── Loads / segments ───────────────────────────────────────────────────

    [RelayCommand]
    private async Task AddRangeAsync()
    {
        // Segment mode edits each row in place and has no shared start/end
        // inputs to read, so Add Segment appends a row to fill in rather than
        // validating two fields that are not on screen.
        if (Session.Mode == TimingMode.Segments)
        {
            PushUndo();
            Session.AddBlankSegment();
            _files.Dirty = true;
            RefreshAll();
            return;
        }

        int start = int.TryParse(RangeStartText, out int s) ? s : 0;
        int end = int.TryParse(RangeEndText, out int e) ? e : 0;

        try
        {
            if (Session.Mode == TimingMode.Loads)
            {
                if (Session.IsConcerninglyLongLoad(start, end))
                {
                    bool proceed = await AppServices.Dialogs.ConfirmAsync(
                        AppServices.Loc["Woah!"],
                        AppServices.Loc["Concerningly Long Load Message"],
                        AppServices.Loc["OK"], AppServices.Loc["Cancel"]);
                    if (!proceed)
                    {
                        return;
                    }
                }
                PushUndo();
                try
                {
                    Session.AddLoad(start, end);
                }
                catch
                {
                    PopUndoDiscard();
                    throw;
                }
                AppServices.MainWindow?.ShowToast(AppServices.Loc["Load added successfully."]);
            }
            else
            {
                PushUndo();
                try
                {
                    Session.AddSegment(start, end);
                }
                catch
                {
                    PopUndoDiscard();
                    throw;
                }
                AppServices.MainWindow?.ShowToast(AppServices.Loc["Segment added successfully."]);
            }

            _files.Dirty = true;
            RangeStartText = "0";
            RangeEndText = "0";
            RefreshAll();
        }
        catch (ValidationException ex)
        {
            await AppServices.Dialogs.ShowErrorAsync(ex.Message);
        }
    }

    /// <summary>Adds a completed range directly (used by the video retimer mark keys).</summary>
    public async Task AddRangeDirectAsync(int start, int end)
    {
        try
        {
            PushUndo();
            try
            {
                if (Session.Mode == TimingMode.Loads)
                {
                    Session.AddLoad(start, end);
                }
                else
                {
                    Session.AddSegment(start, end);
                }
            }
            catch
            {
                PopUndoDiscard();
                throw;
            }
            _files.Dirty = true;
            RefreshAll();
        }
        catch (ValidationException e)
        {
            await AppServices.Dialogs.ShowErrorAsync(e.Message);
        }
    }

    public void SetStartFrame(int frame)
    {
        PushUndo();
        Session.Mutate(startFrame: frame);
        _files.Dirty = true;
        StartFrameText = frame.ToString();
        RefreshAll();
    }

    public void SetEndFrame(int frame)
    {
        PushUndo();
        Session.Mutate(endFrame: frame);
        _files.Dirty = true;
        EndFrameText = frame.ToString();
        RefreshAll();
    }

    public void SetFramerate(decimal framerate)
    {
        PushUndo();
        Session.Mutate(framerate: framerate);
        _files.Dirty = true;
        FramerateText = framerate.ToString(CultureInfo.InvariantCulture);
        RefreshAll();
    }

    public void CommitRow(int index, string startText, string endText)
    {
        _ = CommitRowAsync(index, startText, endText);
    }

    /// <summary>
    /// Pastes the clipboard into one field of an existing row. Segment rows are
    /// edited in place on the retimer page, so each field gets its own Paste
    /// button rather than routing through the shared range inputs.
    /// </summary>
    public async Task PasteRowFieldAsync(int index, bool start)
    {
        if (index < 0 || index >= Rows.Count)
        {
            return;
        }

        var row = Rows[index];
        try
        {
            int frame = await ParseFrameAsync(await ClipboardService.GetTextAsync());
            if (start)
            {
                row.StartText = frame.ToString(CultureInfo.InvariantCulture);
            }
            else
            {
                row.EndText = frame.ToString(CultureInfo.InvariantCulture);
            }
            await CommitRowAsync(index, row.StartText, row.EndText);
        }
        catch (ValidationException ex)
        {
            await AppServices.Dialogs.ShowErrorAsync(ex.Message);
        }
    }

    private async Task CommitRowAsync(int index, string startText, string endText)
    {
        try
        {
            int start = await ParseFrameAsync(startText);
            int end = await ParseFrameAsync(endText);
            PushUndo();
            try
            {
                if (Session.Mode == TimingMode.Loads)
                {
                    if (index >= Session.Loads.Count)
                    {
                        PopUndoDiscard();
                        return;
                    }
                    Session.MutateLoad(index, start, end);
                }
                else
                {
                    if (index >= Session.Segments.Count)
                    {
                        PopUndoDiscard();
                        return;
                    }
                    Session.MutateSegment(index, start, end);
                }
            }
            catch
            {
                PopUndoDiscard();
                throw;
            }
            _files.Dirty = true;
            RefreshAll();
        }
        catch (ValidationException e)
        {
            await AppServices.Dialogs.ShowErrorAsync(e.Message);
            RefreshAll(); // restore row text from the model
        }
    }

    public void DeleteRow(int index)
    {
        PushUndo();
        if (Session.Mode == TimingMode.Loads)
        {
            if (index < 0 || index >= Session.Loads.Count)
            {
                PopUndoDiscard();
                return;
            }
            Session.DeleteLoad(index);
        }
        else
        {
            if (index < 0 || index >= Session.Segments.Count)
            {
                PopUndoDiscard();
                return;
            }
            Session.DeleteSegment(index);
        }
        _files.Dirty = true;
        RefreshAll();
    }

    [RelayCommand]
    private void ClearRows()
    {
        if (Session.Mode == TimingMode.Loads ? Session.Loads.Count == 0 : Session.Segments.Count == 0)
        {
            return;
        }
        PushUndo();
        if (Session.Mode == TimingMode.Loads)
        {
            Session.ClearLoads();
        }
        else
        {
            Session.ClearSegments();
        }
        _files.Dirty = true;
        RefreshAll();
    }

    // ── Mode switching ─────────────────────────────────────────────────────

    public void SetMode(TimingMode mode)
    {
        if (Session.Mode == mode)
        {
            return;
        }
        PushUndo();
        Session.Mode = mode;
        _files.Dirty = true;
        RefreshAll();
        SessionReloaded?.Invoke(this, EventArgs.Empty);
    }

    // ── Undo / redo ────────────────────────────────────────────────────────

    private void PushUndo()
    {
        // Editing after an undo drops the redone branch: the saved state is no
        // longer anywhere on the stack, so its depth marker stops being valid.
        if (_undoStack.Count < _savedUndoDepth)
        {
            _savedUndoDepth = -1;
        }
        _undoStack.Push(Session.Clone());
        _redoStack.Clear();
        UpdateUndoState();
    }

    private void PopUndoDiscard()
    {
        if (_undoStack.Count > 0)
        {
            _undoStack.Pop();
        }
        UpdateUndoState();
    }

    [RelayCommand]
    public void Undo()
    {
        if (_undoStack.Count == 0)
        {
            return;
        }
        _redoStack.Push(Session.Clone());
        _files.ReplaceSession(_undoStack.Pop(), _files.FilePath, dirty: _undoStack.Count != _savedUndoDepth);
        SyncInputsFromSession();
        RefreshAll();
        SessionReloaded?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    public void Redo()
    {
        if (_redoStack.Count == 0)
        {
            return;
        }
        _undoStack.Push(Session.Clone());
        _files.ReplaceSession(_redoStack.Pop(), _files.FilePath, dirty: _undoStack.Count != _savedUndoDepth);
        SyncInputsFromSession();
        RefreshAll();
        SessionReloaded?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateUndoState()
    {
        CanUndo = _undoStack.Count > 0;
        CanRedo = _redoStack.Count > 0;
    }

    // ── Copy actions ───────────────────────────────────────────────────────

    [RelayCommand]
    public async Task CopyModNoteAsync()
    {
        try
        {
            ClipboardService.SetText(ModNoteBuilder.Build(Session, AppServices.Settings.ModNoteFormat));
            AppServices.MainWindow?.ShowToast(AppServices.Loc["Mod note copied"]);
        }
        catch (Exception e)
        {
            await AppServices.Dialogs.ShowErrorAsync(e.Message);
        }
    }

    [RelayCommand]
    public async Task CopyDiscordMessageAsync()
    {
        try
        {
            ClipboardService.SetText(DiscordMessageBuilder.Build(Session));
            AppServices.MainWindow?.ShowToast(AppServices.Loc["Discord message copied"]);
        }
        catch (Exception e)
        {
            await AppServices.Dialogs.ShowErrorAsync(e.Message);
        }
    }

    [RelayCommand]
    public async Task CopyYouTubeChaptersAsync()
    {
        try
        {
            ClipboardService.SetText(YouTubeChaptersBuilder.Build(Session));
            AppServices.MainWindow?.ShowToast(AppServices.Loc["YouTube chapters copied"]);
        }
        catch (Exception e)
        {
            await AppServices.Dialogs.ShowErrorAsync(e.Message);
        }
    }

    [RelayCommand]
    public void CopyPrimaryTime()
    {
        ClipboardService.SetText(TimeFormatter.FormatIso(Session.PrimarySeconds));
        AppServices.MainWindow?.ShowToast(AppServices.Loc["Time copied"]);
    }

    [RelayCommand]
    public void CopySecondaryTime()
    {
        ClipboardService.SetText(TimeFormatter.FormatIso(Session.SecondarySeconds));
        AppServices.MainWindow?.ShowToast(AppServices.Loc["Time copied"]);
    }

    // ── File operations ────────────────────────────────────────────────────

    /// <summary>Port of <c>_prompt_save_if_dirty</c>: returns false when the caller should abort.</summary>
    /// <remarks>
    /// Cancelling the file picker re-asks rather than abandoning the whole
    /// action. Python returned false straight away, which meant backing out of
    /// the picker silently cancelled the New Time / Open the user had asked
    /// for — nothing happened and nothing explained why. Looping keeps the
    /// choice in the user's hands: "Don't Save" still proceeds and "Cancel"
    /// still aborts, so unsaved work is never discarded without saying so.
    /// </remarks>
    public async Task<bool> PromptSaveIfDirtyAsync(string title)
    {
        while (_files.Dirty)
        {
            var choice = await AppServices.Dialogs.PromptSaveAsync(
                title, AppServices.Loc["Would you like to save the current time first?"]);
            if (choice == SavePromptResult.Cancel)
            {
                return false;
            }
            if (choice == SavePromptResult.DontSave)
            {
                return true;
            }

            await SaveAsync();
            // Still dirty means the save was backed out of; ask again.
        }
        return true;
    }

    [RelayCommand]
    public async Task NewTimeAsync()
    {
        if (!await PromptSaveIfDirtyAsync(AppServices.Loc["New Time"]))
        {
            return;
        }
        _files.NewSession(TimingModeExtensions.ParseSerialString(AppServices.Settings.DefaultMode));
        _undoStack.Clear();
        _redoStack.Clear();
        _savedUndoDepth = 0;
        UpdateUndoState();
        SyncInputsFromSession();
        RefreshAll();

        // The video belongs to the run that was just discarded; keeping it open
        // would invite marking a new run against the old footage.
        AppServices.VideoRetimer.CloseVideo();
        SessionReloaded?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    public async Task OpenTimeAsync()
    {
        if (!await PromptSaveIfDirtyAsync(AppServices.Loc["Open Time"]))
        {
            return;
        }

        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".json");
        InitPicker(picker);
        var file = await picker.PickSingleFileAsync();
        if (file is null || file.Path == _files.FilePath)
        {
            return;
        }
        await OpenPathCoreAsync(file.Path);
    }

    /// <summary>Opens a specific path (dashboard/library/history), prompting to save first.</summary>
    public async Task OpenPathAsync(string path)
    {
        if (path == _files.FilePath)
        {
            return;
        }
        if (!await PromptSaveIfDirtyAsync(AppServices.Loc["Open Time"]))
        {
            return;
        }
        await OpenPathCoreAsync(path);
    }

    private async Task OpenPathCoreAsync(string path)
    {
        try
        {
            _files.LoadFile(path);
        }
        catch (ValidationException e)
        {
            await AppServices.Dialogs.ShowErrorAsync(e.Message);
            return;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            await AppServices.Dialogs.ShowErrorAsync(e.Message);
            return;
        }
        _undoStack.Clear();
        _redoStack.Clear();
        _savedUndoDepth = 0;
        UpdateUndoState();
        AppServices.RecentFiles.Touch(path);
        AppServices.Library.Upsert(Session, path);
        SyncInputsFromSession();
        RefreshAll();
        SessionReloaded?.Invoke(this, EventArgs.Empty);

        // Bring back the footage this run was timed against, if it is still
        // there. Awaited last so a slow probe cannot delay showing the times.
        await AppServices.VideoRetimer.RestoreVideoForSessionAsync();
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        if (string.IsNullOrEmpty(_files.FilePath))
        {
            await SaveAsAsync();
            return;
        }
        try
        {
            _files.Save();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ValidationException)
        {
            await AppServices.Dialogs.ShowErrorAsync(e.Message);
            return;
        }
        AfterSave();
    }

    [RelayCommand]
    public async Task SaveAsAsync()
    {
        var picker = new FileSavePicker { SuggestedFileName = "time" };
        picker.FileTypeChoices.Add("Time Files", new List<string> { ".json" });
        InitPicker(picker);
        var file = await picker.PickSaveFileAsync();
        if (file is null)
        {
            return;
        }
        try
        {
            _files.SaveAs(file.Path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            await AppServices.Dialogs.ShowErrorAsync(e.Message);
            return;
        }
        AfterSave();
    }

    private void AfterSave()
    {
        _savedUndoDepth = _undoStack.Count;
        if (_files.FilePath is string path)
        {
            AppServices.RecentFiles.Touch(path);
            AppServices.Library.Upsert(Session, path);
            AppServices.MainWindow?.ShowToast(
                AppServices.Loc.Format("Saved to {path}", ("path", path)));
        }
        RefreshAll();
    }

    // ── Autosave / crash restore ───────────────────────────────────────────

    public void WriteAutosaveIfDirty()
    {
        if (_files.Dirty)
        {
            AppServices.Autosave.Write(Session, _files.FilePath);
        }
    }

    public void RestoreFromSnapshot(AutosaveSnapshot snapshot)
    {
        _files.ReplaceSession(snapshot.Session, snapshot.FilePath, dirty: true);
        _undoStack.Clear();
        _redoStack.Clear();
        _savedUndoDepth = -1; // a restored snapshot has never been saved
        UpdateUndoState();
        SyncInputsFromSession();
        RefreshAll();
        SessionReloaded?.Invoke(this, EventArgs.Empty);
    }

    // ── Display refresh ────────────────────────────────────────────────────

    private void SyncInputsFromSession()
    {
        FramerateText = Session.Framerate.ToString(CultureInfo.InvariantCulture);
        StartFrameText = Session.StartFrame.ToString();
        EndFrameText = Session.EndFrame.ToString();
        RangeStartText = "0";
        RangeEndText = "0";
    }

    public void RefreshAll()
    {
        var loc = AppServices.Loc ?? new Core.Localization.Localizer("en");
        bool segments = Session.Mode == TimingMode.Segments;
        IsSegmentMode = segments;

        PrimaryTimeText = TimeFormatter.FormatIso(Session.PrimarySeconds);
        SecondaryTimeText = TimeFormatter.FormatIso(Session.SecondarySeconds);
        // Both modes read as "Without Loads" / "With Loads". In segment mode the
        // numbers behind them are the segment total and the full-run span, but
        // those are the same two quantities under different names, and the
        // loads wording is what mod notes and speedrun.com use.
        PrimaryLabel = loc["Without Loads"];
        SecondaryLabel = loc["With Loads"];
        RangeStartLabel = segments ? loc["Segment Start"] : loc["Start Frame (Loads)"];
        RangeEndLabel = segments ? loc["Segment End"] : loc["End Frame (Loads)"];
        AddRangeLabel = segments ? loc["Add Segment"] : loc["Add Loads"];
        ClearRowsLabel = segments ? loc["Clear Segments"] : loc["Clear Loads"];

        int count = segments ? Session.Segments.Count : Session.Loads.Count;
        RowsHeader = $"{(segments ? loc["Segments"] : loc["Loads"])} ({count})";
        RowsEmptyText = segments ? loc["No segments yet"] : loc["No loads yet"];
        HasRows = count > 0;
        CanClearRows = count > 0;
        WindowSubtitle = _files.FilePath is null ? "" : Path.GetFileName(_files.FilePath);

        RebuildRows(loc, segments);
        UpdateUndoState();
        SessionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RebuildRows(Core.Localization.Localizer loc, bool segments)
    {
        Rows.Clear();
        if (segments)
        {
            for (int i = 0; i < Session.Segments.Count; i++)
            {
                var segment = Session.Segments[i];
                Rows.Add(new RangeRowViewModel(
                    this, i,
                    $"{loc["Segment"]} {i + 1}",
                    TimeFormatter.FormatFrameTime(segment.Length, Session.Framerate, Session.Precision),
                    segment.StartFrame, segment.EndFrame));
            }
        }
        else
        {
            for (int i = 0; i < Session.Loads.Count; i++)
            {
                var load = Session.Loads[i];
                Rows.Add(new RangeRowViewModel(
                    this, i,
                    $"{loc["Load"]} {i + 1}",
                    TimeFormatter.FormatFrameTime(load.Length, Session.Framerate, Session.Precision),
                    load.StartFrame, load.EndFrame));
            }
        }
    }

    private static void InitPicker(object picker)
    {
        if (AppServices.MainWindow is { } window)
        {
            nint hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        }
    }
}
