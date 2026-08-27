using CRT.Core.Hotkeys;
using CRT.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace CRT.ViewModels;

/// <summary>
/// The hotkey customization dialog (built in code to keep XAML simple): one
/// row per action with live key capture, per-row Reset, Reset All, and
/// duplicate detection with the "Duplicate Hotkey Message" warning.
/// </summary>
public static class HotkeyEditor
{
    /// <summary>Shows the editor; returns the updated map, or null on cancel.</summary>
    public static async Task<Dictionary<string, string>?> ShowAsync(IReadOnlyDictionary<string, string> current)
    {
        var root = AppServices.Dialogs.Root;
        if (root is null)
        {
            return null;
        }

        var loc = AppServices.Loc;
        var working = new Dictionary<string, string>(
            HotkeyRegistry.Actions.ToDictionary(
                a => a.Id,
                a => current.TryGetValue(a.Id, out string? sequence) ? sequence : a.Default));

        var captureButtons = new Dictionary<string, Button>();
        string? capturingActionId = null;

        var list = new StackPanel { Spacing = 4 };

        void RefreshButton(string actionId)
        {
            if (captureButtons.TryGetValue(actionId, out Button? button))
            {
                button.Content = capturingActionId == actionId
                    ? loc["Press a Key Combination"]
                    : (working[actionId].Length > 0 ? working[actionId] : "—");
            }
        }

        foreach (var action in HotkeyRegistry.Actions)
        {
            var grid = new Grid { ColumnSpacing = 8 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var label = new TextBlock
            {
                Text = loc[action.LabelKey],
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            Grid.SetColumn(label, 0);
            grid.Children.Add(label);

            var captureButton = new Button
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Content = working[action.Id],
            };
            string actionId = action.Id;
            captureButton.Click += (_, _) =>
            {
                string? previous = capturingActionId;
                capturingActionId = capturingActionId == actionId ? null : actionId;
                if (previous is not null)
                {
                    RefreshButton(previous);
                }
                RefreshButton(actionId);
            };
            Grid.SetColumn(captureButton, 1);
            grid.Children.Add(captureButton);
            captureButtons[actionId] = captureButton;

            var resetButton = new Button { Content = loc["Reset"] };
            resetButton.Click += (_, _) =>
            {
                working[actionId] = HotkeyRegistry.Defaults[actionId];
                if (capturingActionId == actionId)
                {
                    capturingActionId = null;
                }
                RefreshButton(actionId);
            };
            Grid.SetColumn(resetButton, 2);
            grid.Children.Add(resetButton);

            list.Children.Add(grid);
        }

        var resetAll = new Button
        {
            Content = loc["Reset All"],
            Margin = new Thickness(0, 8, 0, 0),
        };
        resetAll.Click += (_, _) =>
        {
            capturingActionId = null;
            foreach (var action in HotkeyRegistry.Actions)
            {
                working[action.Id] = action.Default;
                RefreshButton(action.Id);
            }
        };

        var content = new StackPanel { Spacing = 4 };
        var scroll = new ScrollViewer
        {
            Content = list,
            MaxHeight = 420,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        content.Children.Add(scroll);
        content.Children.Add(resetAll);

        var dialog = new ContentDialog
        {
            XamlRoot = root,
            Title = loc["Customize Hotkeys"],
            Content = content,
            PrimaryButtonText = loc["OK"],
            CloseButtonText = loc["Cancel"],
            DefaultButton = ContentDialogButton.Primary,
        };

        // Live key capture for the armed row.
        content.PreviewKeyDown += (_, args) =>
        {
            if (capturingActionId is null)
            {
                return;
            }
            var key = args.Key;
            if (KeyGesture.IsModifierKey(key))
            {
                return;
            }
            args.Handled = true;

            var modifiers = VirtualKeyModifiers.None;
            if (IsDown(VirtualKey.Control))
            {
                modifiers |= VirtualKeyModifiers.Control;
            }
            if (IsDown(VirtualKey.Shift))
            {
                modifiers |= VirtualKeyModifiers.Shift;
            }
            if (IsDown(VirtualKey.Menu))
            {
                modifiers |= VirtualKeyModifiers.Menu;
            }

            working[capturingActionId] = KeyGesture.Format(modifiers, key);
            string done = capturingActionId;
            capturingActionId = null;
            RefreshButton(done);
        };

        while (true)
        {
            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
            {
                return null;
            }

            var duplicates = HotkeyRegistry.FindDuplicates(working);
            if (duplicates.Count == 0)
            {
                return working;
            }

            string names = string.Join("; ", duplicates.Select(group =>
                string.Join(", ", group.Select(id =>
                    loc[HotkeyRegistry.Actions.First(a => a.Id == id).LabelKey]))));
            await AppServices.Dialogs.ShowInfoAsync(
                loc["Duplicate Hotkey"],
                loc["Duplicate Hotkey Message"].Replace("{names}", names));
            // Re-show the editor so the user can fix the clash.
        }

        static bool IsDown(VirtualKey key) =>
            Microsoft.UI.Input.InputKeyboardSource
                .GetKeyStateForCurrentThread(key)
                .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
    }
}
