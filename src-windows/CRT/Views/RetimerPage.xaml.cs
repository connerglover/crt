using System.Globalization;
using CRT.Core.Models;
using CRT.Services;
using CRT.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace CRT.Views;

/// <summary>
/// The frame retimer — parity with the Qt main window: menu bar, click-to-copy
/// time cards, parsed input rows with paste buttons, loads/segments sidebar,
/// plus the new mode switch and undo/redo.
/// </summary>
public sealed partial class RetimerPage : Page
{
    private static readonly string[] FramerateQuickPicks =
        { "24", "25", "29.97", "30", "50", "59.94", "60" };

    private bool _suppressModeEvent;

    public RetimerPage()
    {
        InitializeComponent();
        ApplyLocalization();
        BuildFramerateQuickPicks();
        BuildAccelerators();

        VM.SessionReloaded += (_, _) => SyncModeSelector();
        Loaded += (_, _) => SyncModeSelector();
    }

    public SessionViewModel VM => AppServices.Session;

    public Visibility Invert(bool value) => value ? Visibility.Collapsed : Visibility.Visible;

    // ── Setup ──────────────────────────────────────────────────────────────

    private void ApplyLocalization()
    {
        var loc = AppServices.Loc;

        FramerateLabel.Text = loc["Framerate"];
        StartFrameLabel.Text = loc["Start Frame"];
        EndFrameLabel.Text = loc["End Frame"];
        StartPasteButton.Content = loc["Paste"];
        EndPasteButton.Content = loc["Paste"];
        RangeStartPasteButton.Content = loc["Paste"];
        RangeEndPasteButton.Content = loc["Paste"];
        CopyModNoteButton.Content = loc["Copy Mod Note"];
        FlyoutCopyDiscord.Text = loc["Copy Discord Message"];
        FlyoutCopyChapters.Text = loc["Copy YouTube Chapters"];

        ToolTipService.SetToolTip(SidebarToggle, loc["Loads"]);

        _suppressModeEvent = true;
        ModeSelector.Items.Clear();
        ModeSelector.Items.Add(loc["Load Mode"]);
        ModeSelector.Items.Add(loc["Segment Mode"]);
        _suppressModeEvent = false;
    }

    private void BuildFramerateQuickPicks()
    {
        foreach (string pick in FramerateQuickPicks)
        {
            var item = new MenuFlyoutItem { Text = pick };
            item.Click += (_, _) =>
                VM.SetFramerate(decimal.Parse(pick, CultureInfo.InvariantCulture));
            FramerateQuickFlyout.Items.Add(item);
        }
    }

    private void BuildAccelerators()
    {
        var hotkeys = AppServices.Settings.Hotkeys;

        void Add(string actionId, Action action)
        {
            if (hotkeys.TryGetValue(actionId, out string? gesture) &&
                KeyGesture.CreateAccelerator(gesture, action) is { } accelerator)
            {
                KeyboardAccelerators.Add(accelerator);
            }
        }

        Add("New Time", () => _ = VM.NewTimeAsync());
        Add("Open Time", () => _ = VM.OpenTimeAsync());
        Add("Session History", () => _ = MenuBarControl.ShowSessionHistoryAsync());
        Add("Save", () => _ = VM.SaveAsync());
        Add("Save As", () => _ = VM.SaveAsAsync());
        Add("Settings", () => AppServices.MainWindow?.NavigateTo("settings"));
        Add("Copy Mod Note", () => _ = VM.CopyModNoteAsync());
        Add("Copy Discord Message", () => _ = VM.CopyDiscordMessageAsync());
        Add("Copy YouTube Chapters", () => _ = VM.CopyYouTubeChaptersAsync());
        Add("Clear Loads", () => VM.ClearRowsCommand.Execute(null));
        Add("start_paste", () => _ = VM.PasteStartFrameAsync());
        Add("end_paste", () => _ = VM.PasteEndFrameAsync());
        Add("start_loads_paste", () => _ = VM.PasteRangeStartAsync());
        Add("end_loads_paste", () => _ = VM.PasteRangeEndAsync());
        Add("Add Loads", () => _ = AddRangeFromFocusedAsync());
        Add("Toggle Mode", VM.ToggleMode);

        // Fixed bindings (spec §14): undo/redo + quick time copy.
        AddFixed("Ctrl+Z", VM.Undo);
        AddFixed("Ctrl+Shift+Z", VM.Redo);
        AddFixed("Ctrl+Y", VM.Redo);
        AddFixed("Ctrl+Shift+C", VM.CopyPrimaryTime);

        void AddFixed(string gesture, Action action)
        {
            if (KeyGesture.CreateAccelerator(gesture, action) is { } accelerator)
            {
                KeyboardAccelerators.Add(accelerator);
            }
        }
    }

    private void SyncModeSelector()
    {
        _suppressModeEvent = true;
        ModeSelector.SelectedIndex = VM.IsSegmentMode ? 1 : 0;
        _suppressModeEvent = false;
    }

    // ── Mode / sidebar ─────────────────────────────────────────────────────

    private void OnModeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressModeEvent || ModeSelector.SelectedIndex < 0)
        {
            return;
        }
        VM.SetMode(ModeSelector.SelectedIndex == 1 ? TimingMode.Segments : TimingMode.Loads);
    }

    private void OnSidebarToggle(object sender, RoutedEventArgs e)
    {
        bool visible = SidebarToggle.IsChecked == true;
        Sidebar.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        SidebarColumn.Width = visible ? new GridLength(280) : new GridLength(0);
    }

    // ── Time cards ─────────────────────────────────────────────────────────

    private void OnPrimaryCardClick(object sender, RoutedEventArgs e) => VM.CopyPrimaryTime();

    private void OnSecondaryCardClick(object sender, RoutedEventArgs e) => VM.CopySecondaryTime();

    // ── Input commits ──────────────────────────────────────────────────────

    private void OnInputKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != Windows.System.VirtualKey.Enter)
        {
            return;
        }
        e.Handled = true;
        CommitBox((TextBox)sender);
    }

    private void CommitBox(TextBox box)
    {
        if (box == FramerateBox)
        {
            VM.CommitFramerate(box.Text);
        }
        else if (box == StartFrameBox)
        {
            _ = VM.CommitStartFrameAsync(box.Text);
        }
        else if (box == EndFrameBox)
        {
            _ = VM.CommitEndFrameAsync(box.Text);
        }
        else if (box == RangeStartBox)
        {
            _ = VM.CommitRangeFieldAsync(true, box.Text);
        }
        else if (box == RangeEndBox)
        {
            _ = VM.CommitRangeFieldAsync(false, box.Text);
        }
    }

    private void OnFramerateCommit(object sender, RoutedEventArgs e) => VM.CommitFramerate(FramerateBox.Text);

    private void OnStartFrameCommit(object sender, RoutedEventArgs e) => _ = VM.CommitStartFrameAsync(StartFrameBox.Text);

    private void OnEndFrameCommit(object sender, RoutedEventArgs e) => _ = VM.CommitEndFrameAsync(EndFrameBox.Text);

    private void OnRangeStartCommit(object sender, RoutedEventArgs e) => _ = VM.CommitRangeFieldAsync(true, RangeStartBox.Text);

    private void OnRangeEndCommit(object sender, RoutedEventArgs e) => _ = VM.CommitRangeFieldAsync(false, RangeEndBox.Text);

    // ── Paste buttons ──────────────────────────────────────────────────────

    private void OnPasteStart(object sender, RoutedEventArgs e) => _ = VM.PasteStartFrameAsync();

    private void OnPasteEnd(object sender, RoutedEventArgs e) => _ = VM.PasteEndFrameAsync();

    private void OnPasteRangeStart(object sender, RoutedEventArgs e) => _ = VM.PasteRangeStartAsync();

    private void OnPasteRangeEnd(object sender, RoutedEventArgs e) => _ = VM.PasteRangeEndAsync();

    // ── Copy / add actions ─────────────────────────────────────────────────

    private void OnCopyModNote(SplitButton sender, SplitButtonClickEventArgs args) =>
        _ = VM.CopyModNoteAsync();

    private void OnMenuCopyDiscord(object sender, RoutedEventArgs e) => _ = VM.CopyDiscordMessageAsync();

    private void OnMenuCopyChapters(object sender, RoutedEventArgs e) => _ = VM.CopyYouTubeChaptersAsync();

    /// <summary>
    /// The Add Loads hotkey must use what is currently typed: an accelerator
    /// does not move focus, so the focused range field is committed through the
    /// §2 parser first (the button path gets this for free via LostFocus).
    /// </summary>
    private async Task AddRangeFromFocusedAsync()
    {
        if (XamlRoot is not null && FocusManager.GetFocusedElement(XamlRoot) is TextBox box)
        {
            if (box == RangeStartBox)
            {
                await VM.CommitRangeFieldAsync(true, box.Text);
            }
            else if (box == RangeEndBox)
            {
                await VM.CommitRangeFieldAsync(false, box.Text);
            }
        }
        VM.AddRangeCommand.Execute(null);
    }
}
