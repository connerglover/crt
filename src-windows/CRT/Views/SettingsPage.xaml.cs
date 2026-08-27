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
    }

    public SettingsViewModel VM { get; }

    private void ApplyLocalization()
    {
        var loc = AppServices.Loc;
        PageHeader.Text = loc["CRT Settings"];
        UpdatesCheck.Content = loc["Automatically Check for Updates"];
        ThemeLabel.Text = loc["Theme"];
        AccentLabel.Text = loc["Accent Color"];
        LanguageLabel.Text = loc["Language"];
        ModNoteLabel.Text = loc["Mod Note Format"];
        TimerCornerLabel.Text = loc["Timer Corner"];
        TimerStyleLabel.Text = loc["Timer Style"];
        FfmpegLabel.Text = loc["FFmpeg Path"];
        YtDlpLabel.Text = loc["yt-dlp Path"];
        ClassicModeCheck.Content = loc["Classic Mode"];
        ClassicModeHint.Text = loc["Classic Mode Description"];
        DualTimerCheck.Content = loc["Dual Timer"];
        DualTimerHint.Text = loc["Dual Timer Description"];
        HotkeysButton.Content = loc["Customize Hotkeys"];
        ApplyButton.Content = loc["Apply"];
        CancelButton.Content = loc["Cancel"];
        RestoreButton.Content = loc["Restore Defaults"];
        PickColorButton.Content = "🎨";

        Fill(ThemeCombo, VM.ThemeOptions);
        Fill(LanguageCombo, VM.LanguageOptions);
        Fill(TimerCornerCombo, VM.TimerCornerOptions);
        Fill(TimerStyleCombo, VM.TimerStyleOptions);

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
}
