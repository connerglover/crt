using CRT.Core.Models;
using CRT.Services;
using CRT.ViewModels;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace CRT.Views;

/// <summary>
/// The video retimer workspace: import box, frame-accurate player, timeline
/// with marked-region overlay, transport + mark controls, and export.
/// </summary>
public sealed partial class VideoRetimerPage : Page
{
    private DispatcherQueueTimer? _positionTimer;
    private PageHotkeys _hotkeys = null!;
    private bool _suppressSpeedEvent;
    private bool _suppressSliderEvent;
    private bool _sliderDragging;

    public VideoRetimerPage()
    {
        InitializeComponent();
        ApplyLocalization();
        BuildAccelerators();

        PlayerElement.SetMediaPlayer(VM.Player);

        // The Slider marks its own pointer events handled, so the drag guard has
        // to be attached with handledEventsToo to ever see them.
        TimelineSlider.AddHandler(PointerPressedEvent, new PointerEventHandler(OnSliderPressed), true);
        TimelineSlider.AddHandler(PointerReleasedEvent, new PointerEventHandler(OnSliderReleased), true);

        SessionVM.SessionChanged += (_, _) => RedrawRegions();
        Loaded += OnPageLoaded;
        Unloaded += OnPageUnloaded;
    }

    public VideoRetimerViewModel VM => AppServices.VideoRetimer;

    public SessionViewModel SessionVM => AppServices.Session;

    public Visibility Invert(bool value) => value ? Visibility.Collapsed : Visibility.Visible;

    private void ApplyLocalization()
    {
        var loc = AppServices.Loc;
        ImportBox.PlaceholderText = loc["Video URL"];
        ImportButton.Content = loc["Import"];
        BrowseButton.Content = loc["Browse"];
        EmptyHint.Text = loc["No video loaded"];
        PlayPauseButton.Content = loc["Play"];
        FrameReadoutLabel.Text = loc["Current Frame"];
        TimeReadoutLabel.Text = loc["Current Time"];
        MarkLoadStartButton.Content = loc["Mark Load Start"];
        MarkLoadEndButton.Content = loc["Mark Load End"];
        ExportButton.Content = loc["Export Retimed Video"];
        ToolTipService.SetToolTip(ScanBackButton, loc["Rewind"]);
        ToolTipService.SetToolTip(ScanForwardButton, loc["Fast Forward"]);
        ToolTipService.SetToolTip(SpeedCombo, loc["Playback Speed"]);
        BuildSpeedPicker();
        UpdateMarkButtonLabels();
    }

    private void BuildSpeedPicker()
    {
        _suppressSpeedEvent = true;
        SpeedCombo.Items.Clear();
        foreach (double rate in VideoRetimerViewModel.PlaybackRates)
        {
            SpeedCombo.Items.Add(FormatRate(rate));
        }
        SpeedCombo.SelectedIndex = VideoRetimerViewModel.PlaybackRates.ToList().IndexOf(VM.PlaybackRate);
        _suppressSpeedEvent = false;
    }

    private static string FormatRate(double rate) =>
        rate.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "x";

    private void OnSpeedChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressSpeedEvent || SpeedCombo.SelectedIndex < 0)
        {
            return;
        }
        VM.SetPlaybackRate(VideoRetimerViewModel.PlaybackRates[SpeedCombo.SelectedIndex]);
    }

    private void UpdateMarkButtonLabels()
    {
        var loc = AppServices.Loc;
        bool segments = SessionVM.IsSegmentMode;
        MarkStartButton.Content = segments ? loc["Mark Segment Start"] : loc["Mark Run Start"];
        MarkEndButton.Content = segments ? loc["Mark Segment End"] : loc["Mark Run End"];
    }

    private void BuildAccelerators()
    {
        var hotkeys = AppServices.Settings.Hotkeys;
        _hotkeys = new PageHotkeys(this);

        void Add(string actionId, Action action) => _hotkeys.Bind(hotkeys, actionId, action);

        // Video-mode actions (spec §9.2/§10).
        Add("video_frame_back", VM.StepBackward);
        Add("video_frame_forward", VM.StepForward);
        Add("video_play_pause", VM.PlayPause);
        Add("video_mark_start", () => _ = VM.MarkStartAsync());
        Add("video_mark_end", () => _ = VM.MarkEndAsync());
        Add("video_mark_load_start", () => _ = VM.MarkLoadStartAsync());
        Add("video_mark_load_end", () => _ = VM.MarkLoadEndAsync());

        // Shift variants of , and . step too (spec: "< / >").
        AddFixed("Shift+,", VM.StepBackward);
        AddFixed("Shift+.", VM.StepForward);
        // Arrows: ±5 frames; Shift+Arrows: ±1 second.
        AddFixed("Left", () => VM.JumpFrames(-5));
        AddFixed("Right", () => VM.JumpFrames(5));
        AddFixed("Shift+Left", () => VM.JumpSeconds(-1m));
        AddFixed("Shift+Right", () => VM.JumpSeconds(1m));
        // Shuttle. The usual J/K/L is unavailable: L and Shift+L are already the
        // load-mark keys, so scanning uses the arrow pair it sits next to.
        AddFixed("Ctrl+Left", VM.ScanBackward);
        AddFixed("Ctrl+Right", VM.ScanForward);
        AddFixed("K", VM.StopScan);
        // Shared copy/save shortcuts stay available here.
        AddFixed("Ctrl+Z", SessionVM.Undo);
        AddFixed("Ctrl+Shift+Z", SessionVM.Redo);
        AddFixed("Ctrl+Shift+C", SessionVM.CopyPrimaryTime);
        Add("Save", () => _ = SessionVM.SaveAsync());
        Add("Copy Mod Note", () => _ = SessionVM.CopyModNoteAsync());

        void AddFixed(string gesture, Action action) => _hotkeys.Bind(gesture, action);
    }

    // ── Lifecycle ──────────────────────────────────────────────────────────

    private void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        _positionTimer ??= DispatcherQueue.CreateTimer();
        _positionTimer.Interval = TimeSpan.FromMilliseconds(100);
        _positionTimer.Tick += OnPositionTick;
        _positionTimer.Start();
        UpdateMarkButtonLabels();
        RedrawRegions();
    }

    private void OnPageUnloaded(object sender, RoutedEventArgs e)
    {
        if (_positionTimer is not null)
        {
            _positionTimer.Stop();
            _positionTimer.Tick -= OnPositionTick;
        }
    }

    private void OnPositionTick(DispatcherQueueTimer sender, object args)
    {
        if (_sliderDragging)
        {
            return;
        }
        _suppressSliderEvent = true;
        VM.UpdatePosition();
        _suppressSliderEvent = false;
        PlayPauseButton.Content = VM.IsPlaying ? AppServices.Loc["Pause"] : AppServices.Loc["Play"];
        UpdateMarkButtonLabels();
    }

    // ── Import ─────────────────────────────────────────────────────────────

    private void OnImportKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            e.Handled = true;
            VM.ImportCommand.Execute(null);
        }
    }

    // ── Timeline ───────────────────────────────────────────────────────────

    private void OnSliderValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_suppressSliderEvent || !VM.HasVideo)
        {
            return;
        }
        VM.SeekSeconds((decimal)e.NewValue);
    }

    /// <summary>While the thumb is held the position timer must not fight the user.</summary>
    private void OnSliderPressed(object sender, PointerRoutedEventArgs e) => _sliderDragging = true;

    private void OnSliderReleased(object sender, PointerRoutedEventArgs e) => _sliderDragging = false;

    private void OnRegionsCanvasSizeChanged(object sender, SizeChangedEventArgs e) => RedrawRegions();

    /// <summary>Draws marked segments/loads as colored regions above the timeline.</summary>
    private void RedrawRegions()
    {
        RegionsCanvas.Children.Clear();
        double width = RegionsCanvas.ActualWidth;
        if (width <= 0 || !VM.HasVideo || VM.DurationSeconds <= 0m)
        {
            return;
        }

        var session = SessionVM.Session;
        decimal fps = VM.Fps;
        if (fps == 0m)
        {
            return;
        }
        double duration = (double)VM.DurationSeconds;

        var accent = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
        var muted = new SolidColorBrush(Windows.UI.Color.FromArgb(120, 128, 128, 128));

        void AddRegion(int startFrame, int endFrame, Microsoft.UI.Xaml.Media.Brush brush)
        {
            double from = Math.Clamp((double)(startFrame / fps) / duration, 0, 1) * width;
            double to = Math.Clamp((double)(endFrame / fps) / duration, 0, 1) * width;
            if (to <= from)
            {
                return;
            }
            var rect = new Rectangle
            {
                Width = to - from,
                Height = RegionsCanvas.ActualHeight > 0 ? RegionsCanvas.ActualHeight : 8,
                Fill = brush,
                RadiusX = 2,
                RadiusY = 2,
            };
            Canvas.SetLeft(rect, from);
            RegionsCanvas.Children.Add(rect);
        }

        if (session.Mode == TimingMode.Segments)
        {
            foreach (var segment in session.Segments)
            {
                AddRegion(segment.StartFrame, segment.EndFrame, accent);
            }
        }
        else
        {
            if (session.EndFrame > session.StartFrame)
            {
                AddRegion(session.StartFrame, session.EndFrame, accent);
            }
            foreach (var load in session.Loads)
            {
                AddRegion(load.StartFrame, load.EndFrame, muted);
            }
        }
    }
}
