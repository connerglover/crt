using CRT.Services;
using CRT.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace CRT.Views;

/// <summary>Settings: Apply / Cancel / Restore Defaults + hotkey customization.</summary>
public sealed partial class SettingsPage : Page
{
    private bool _suppressPickerEvent;

    public SettingsPage()
    {
        VM = new SettingsViewModel();
        InitializeComponent();
        ApplyLocalization();
        UpdateSwatch();
        UpdateTimerSwatches();

        // The settings page is cached like the others, so it re-reads its own
        // labels and refills its pickers when the language changes — including
        // when the change was made here.
        AppServices.SettingsChanged += (_, _) => ApplyLocalization();
    }

    public SettingsViewModel VM { get; }

    private void ApplyLocalization()
    {
        var loc = AppServices.Loc;
        PageHeader.Text = loc["CRT Settings"];
        GeneralHeader.Text = loc["General"];
        TimingHeader.Text = loc["Timing"];
        TimerContentHeader.Text = loc["Timer Content"];
        TimerTextHeader.Text = loc["Timer Text"];
        TimerBackgroundHeader.Text = loc["Timer Background Section"];
        TimerLayoutHeader.Text = loc["Timer Placement"];
        ToolsHeader.Text = loc["Tools"];
        ShortcutsHeader.Text = loc["Hotkeys"];
        TimerOutlineLabel.Text = loc["Outline"];
        TimerOutlineColorLabel.Text = loc["Outline Color"];
        TimerCornersLabel.Text = loc["Rounded Corners"];
        UpdatesCheck.Content = loc["Automatically Check for Updates"];
        ThemeLabel.Text = loc["Theme"];
        AccentLabel.Text = loc["Accent Color"];
        LanguageLabel.Text = loc["Language"];
        ModNoteLabel.Text = loc["Mod Note Format"];
        TimerCornerLabel.Text = loc["Timer Corner"];
        TimerHeader.Text = loc["In-Video Timer"];
        TimerPresetLabel.Text = loc["Preset"];
        TimerFormatLabel.Text = loc["Timer Format"];
        TimerFormatHint.Text = loc["Timer Format Hint"];
        ClockStyleLabel.Text = loc["Clock Style"];
        ClockStyleHint.Text = loc["Clock Style Hint"];
        TimerFontLabel.Text = loc["Font"];
        TimerSizeLabel.Text = loc["Text Size"];
        TimerSpacingLabel.Text = loc["Line Spacing"];
        PreviewTimerButton.Content = loc["Preview Timer"];
        TimerTextColorLabel.Text = loc["Text Color"];
        TimerBackgroundCheck.Content = loc["Timer Background"];
        TimerBackgroundColorLabel.Text = loc["Background Color"];
        TimerOpacityLabel.Text = loc["Background Opacity"];
        TimerTextColorButton.Content = loc["Pick"];
        TimerBackgroundColorButton.Content = loc["Pick"];
        TimerOutlineColorButton.Content = loc["Pick"];
        FfmpegLabel.Text = loc["FFmpeg Path"];
        YtDlpLabel.Text = loc["yt-dlp Path"];
        ClassicModeCheck.Content = loc["Classic Mode"];
        ClassicModeHint.Text = loc["Classic Mode Description"];
        HotkeysButton.Content = loc["Customize Hotkeys"];
        ApplyButton.Content = loc["Apply"];
        CancelButton.Content = loc["Cancel"];
        RestoreButton.Content = loc["Restore Defaults"];
        PickColorButton.Content = "🎨";

        Fill(ThemeCombo, VM.ThemeOptions);
        Fill(LanguageCombo, VM.LanguageOptions);
        Fill(TimerCornerCombo, VM.TimerCornerOptions);
        Fill(ClockStyleCombo, VM.ClockStyleOptions);
        Fill(TimerPresetCombo, VM.TimerPresetOptions);
        // Bound rather than copied: the template needs the family name as the
        // item so it can draw each entry in its own font.
        TimerFontCombo.ItemsSource = VM.TimerFontOptions;
        Fill(TimerWeightCombo, VM.TimerWeightOptions);

        // Re-apply indexes after filling (SelectedIndex resets when items change).
        VM.CancelCommand.Execute(null);

        static void Fill(ComboBox combo, IReadOnlyList<string> options)
        {
            combo.Items.Clear();
            foreach (string option in options)
            {
                combo.Items.Add(option);
            }
        }
    }

    private void OnAccentTextChanged(object sender, TextChangedEventArgs e) => UpdateSwatch();

    private void OnAccentPicked(ColorPicker sender, ColorChangedEventArgs args)
    {
        if (_suppressPickerEvent)
        {
            return;
        }
        VM.AccentColor = ThemeService.FormatHexColor(args.NewColor);
        UpdateSwatch();
    }

    private void UpdateSwatch()
    {
        if (ThemeService.TryParseHexColor(VM.AccentColor, out Windows.UI.Color color))
        {
            AccentSwatch.Background = new SolidColorBrush(color);
            _suppressPickerEvent = true;
            AccentPicker.Color = color;
            _suppressPickerEvent = false;
        }
    }

    // ── Timer colors ───────────────────────────────────────────────────────

    private void OnTimerTextColorChanged(object sender, TextChangedEventArgs e) => UpdateTimerSwatches();

    private void OnTimerBackgroundColorChanged(object sender, TextChangedEventArgs e) => UpdateTimerSwatches();

    private void OnTimerOutlineColorChanged(object sender, TextChangedEventArgs e) => UpdateTimerSwatches();

    private void OnTimerOutlineColorPicked(ColorPicker sender, ColorChangedEventArgs args)
    {
        if (_suppressPickerEvent)
        {
            return;
        }
        VM.TimerOutlineColor = ThemeService.FormatHexColor(args.NewColor);
        UpdateTimerSwatches();
    }

    private void OnTimerTextColorPicked(ColorPicker sender, ColorChangedEventArgs args)
    {
        if (_suppressPickerEvent)
        {
            return;
        }
        VM.TimerTextColor = ThemeService.FormatHexColor(args.NewColor);
        UpdateTimerSwatches();
    }

    private void OnTimerBackgroundColorPicked(ColorPicker sender, ColorChangedEventArgs args)
    {
        if (_suppressPickerEvent)
        {
            return;
        }
        VM.TimerBackgroundColor = ThemeService.FormatHexColor(args.NewColor);
        UpdateTimerSwatches();
    }

    private void UpdateTimerSwatches()
    {
        _suppressPickerEvent = true;
        if (ThemeService.TryParseHexColor(VM.TimerTextColor, out Windows.UI.Color text))
        {
            TimerTextSwatch.Background = new SolidColorBrush(text);
            TimerTextPicker.Color = text;
        }
        if (ThemeService.TryParseHexColor(VM.TimerBackgroundColor, out Windows.UI.Color background))
        {
            TimerBackgroundSwatch.Background = new SolidColorBrush(background);
            TimerBackgroundPicker.Color = background;
        }
        if (ThemeService.TryParseHexColor(VM.TimerOutlineColor, out Windows.UI.Color outline))
        {
            TimerOutlineSwatch.Background = new SolidColorBrush(outline);
            TimerOutlinePicker.Color = outline;
        }
        _suppressPickerEvent = false;
    }
}
