using CRT.Core;
using CRT.Services;
using CRT.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CRT.Views;

/// <summary>
/// The in-window menu bar required by spec §6 on both retimer pages (§13.1):
/// File / Edit / View / Help over the one shared session.
/// </summary>
public sealed partial class RetimerMenuBar : UserControl
{
    public RetimerMenuBar()
    {
        InitializeComponent();
        ApplyLocalization();

        Loaded += (_, _) => MenuAlwaysOnTop.IsChecked = AppServices.MainWindow?.AlwaysOnTop ?? true;
    }

    public SessionViewModel VM => AppServices.Session;

    // ── Setup ──────────────────────────────────────────────────────────────

    private void ApplyLocalization()
    {
        var loc = AppServices.Loc;
        var hotkeys = AppServices.Settings.Hotkeys;

        FileMenu.Title = loc["File"];
        EditMenu.Title = loc["Edit (Menu Bar)"];
        ViewMenu.Title = loc["View"];
        HelpMenu.Title = loc["Help"];

        MenuNewTime.Text = loc["New Time"];
        MenuOpenTime.Text = loc["Open Time"];
        MenuSessionHistory.Text = loc["Session History"];
        MenuSave.Text = loc["Save"];
        MenuSaveAs.Text = loc["Save As"];
        MenuSettings.Text = loc["Settings"];
        MenuExit.Text = loc["Exit"];
        MenuUndo.Text = loc["Undo"];
        MenuRedo.Text = loc["Redo"];
        MenuCopyModNote.Text = loc["Copy Mod Note"];
        MenuCopyDiscord.Text = loc["Copy Discord Message"];
        MenuCopyChapters.Text = loc["Copy YouTube Chapters"];
        MenuClearLoads.Text = loc["Clear Loads"];
        MenuAlwaysOnTop.Text = loc["Always on Top"];
        MenuAbout.Text = loc["About"];

        // Shortcut hints beside the menu entries come from the configured hotkeys.
        MenuNewTime.KeyboardAcceleratorTextOverride = Get(hotkeys, "New Time");
        MenuOpenTime.KeyboardAcceleratorTextOverride = Get(hotkeys, "Open Time");
        MenuSessionHistory.KeyboardAcceleratorTextOverride = Get(hotkeys, "Session History");
        MenuSave.KeyboardAcceleratorTextOverride = Get(hotkeys, "Save");
        MenuSaveAs.KeyboardAcceleratorTextOverride = Get(hotkeys, "Save As");
        MenuSettings.KeyboardAcceleratorTextOverride = Get(hotkeys, "Settings");
        MenuCopyModNote.KeyboardAcceleratorTextOverride = Get(hotkeys, "Copy Mod Note");
        MenuCopyDiscord.KeyboardAcceleratorTextOverride = Get(hotkeys, "Copy Discord Message");
        MenuCopyChapters.KeyboardAcceleratorTextOverride = Get(hotkeys, "Copy YouTube Chapters");
        MenuClearLoads.KeyboardAcceleratorTextOverride = Get(hotkeys, "Clear Loads");
        MenuUndo.KeyboardAcceleratorTextOverride = "Ctrl+Z";
        MenuRedo.KeyboardAcceleratorTextOverride = "Ctrl+Shift+Z";

        static string Get(IReadOnlyDictionary<string, string> hotkeys, string id) =>
            hotkeys.TryGetValue(id, out string? sequence) ? sequence : "";
    }

    // ── Menu handlers ──────────────────────────────────────────────────────

    private void OnMenuNewTime(object sender, RoutedEventArgs e) => _ = VM.NewTimeAsync();

    private void OnMenuOpenTime(object sender, RoutedEventArgs e) => _ = VM.OpenTimeAsync();

    private void OnMenuSessionHistory(object sender, RoutedEventArgs e) => _ = ShowSessionHistoryAsync();

    private void OnMenuSave(object sender, RoutedEventArgs e) => _ = VM.SaveAsync();

    private void OnMenuSaveAs(object sender, RoutedEventArgs e) => _ = VM.SaveAsAsync();

    private void OnMenuSettings(object sender, RoutedEventArgs e) =>
        AppServices.MainWindow?.NavigateTo("settings");

    private void OnMenuExit(object sender, RoutedEventArgs e) => AppServices.MainWindow?.Close();

    private void OnMenuUndo(object sender, RoutedEventArgs e) => VM.Undo();

    private void OnMenuRedo(object sender, RoutedEventArgs e) => VM.Redo();

    private void OnMenuCopyModNote(object sender, RoutedEventArgs e) => _ = VM.CopyModNoteAsync();

    private void OnMenuCopyDiscord(object sender, RoutedEventArgs e) => _ = VM.CopyDiscordMessageAsync();

    private void OnMenuCopyChapters(object sender, RoutedEventArgs e) => _ = VM.CopyYouTubeChaptersAsync();

    private void OnMenuClearLoads(object sender, RoutedEventArgs e) => VM.ClearRowsCommand.Execute(null);

    private void OnMenuAlwaysOnTop(object sender, RoutedEventArgs e) =>
        AppServices.MainWindow?.SetAlwaysOnTop(MenuAlwaysOnTop.IsChecked);

    private void OnMenuAbout(object sender, RoutedEventArgs e)
    {
        _ = AppServices.Dialogs.ShowInfoAsync(
            AppServices.Loc["About"],
            $"Conner's Retime Tool v{AppVersion.Version}\n\n" +
            "Created by Conner Glover\n\n" +
            "Credits:\nMenzo: French and Polish Translations\n" +
            "AmazinCris: Spanish Translations\n\n" +
            "© 2026 Conner Glover");
    }

    // ── Session history ────────────────────────────────────────────────────

    /// <summary>
    /// Spec §6: this session's opened/saved paths plus the persisted recent
    /// files, de-duplicated, so the list survives a restart.
    /// </summary>
    public async Task ShowSessionHistoryAsync()
    {
        var paths = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string path in VM.History.Concat(AppServices.RecentFiles.Paths))
        {
            if (seen.Add(path) && File.Exists(path))
            {
                paths.Add(path);
            }
        }
        if (paths.Count == 0)
        {
            await AppServices.Dialogs.ShowInfoAsync(
                AppServices.Loc["Session History"], AppServices.Loc["Empty Library"]);
            return;
        }

        var list = new ListView
        {
            SelectionMode = ListViewSelectionMode.Single,
            MaxHeight = 320,
        };
        foreach (string path in paths)
        {
            list.Items.Add(new TextBlock
            {
                Text = path,
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
        }

        var dialog = new ContentDialog
        {
            XamlRoot = AppServices.Dialogs.Root,
            Title = AppServices.Loc["Session History"],
            Content = list,
            PrimaryButtonText = AppServices.Loc["Open Time"],
            CloseButtonText = AppServices.Loc["Cancel"],
            DefaultButton = ContentDialogButton.Primary,
        };
        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary && list.SelectedIndex >= 0)
        {
            await VM.OpenPathAsync(paths[list.SelectedIndex]);
        }
    }
}
