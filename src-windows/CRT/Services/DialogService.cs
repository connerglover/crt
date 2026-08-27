using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CRT.Services;

/// <summary>Result of a Save / Don't Save / Cancel prompt.</summary>
public enum SavePromptResult
{
    Save,
    DontSave,
    Cancel,
}

/// <summary>Result of a two-action dialog that can also be dismissed without acting.</summary>
public enum DialogChoice
{
    Primary,
    Secondary,
    Dismissed,
}

/// <summary>
/// Central dialog helper. All dialogs are queued so two can never be open at
/// once (ContentDialog throws on concurrent opens).
/// </summary>
public sealed class DialogService
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>Set by the main window once its content is loaded.</summary>
    public XamlRoot? Root { get; set; }

    public async Task ShowErrorAsync(string message)
    {
        await ShowAsync(AppServices.Loc["Error"], message, AppServices.Loc["OK"], null, null);
    }

    public async Task ShowInfoAsync(string title, string message)
    {
        await ShowAsync(title, message, AppServices.Loc["OK"], null, null);
    }

    public async Task<bool> ConfirmAsync(string title, string message, string? yesText = null, string? noText = null)
    {
        var result = await ShowAsync(
            title, message,
            yesText ?? AppServices.Loc["OK"],
            noText ?? AppServices.Loc["Cancel"],
            null);
        return result == ContentDialogResult.Primary;
    }

    /// <summary>
    /// Offers two actions plus a close button, so Escape (or the close button)
    /// dismisses without performing either one.
    /// </summary>
    public async Task<DialogChoice> ChooseAsync(
        string title, string message, string primaryText, string secondaryText, string? closeText = null)
    {
        var result = await ShowAsync(
            title, message, primaryText, secondaryText, closeText ?? AppServices.Loc["Cancel"]);
        return result switch
        {
            ContentDialogResult.Primary => DialogChoice.Primary,
            ContentDialogResult.Secondary => DialogChoice.Secondary,
            _ => DialogChoice.Dismissed,
        };
    }

    public async Task<SavePromptResult> PromptSaveAsync(string title, string message)
    {
        var result = await ShowAsync(
            title, message,
            AppServices.Loc["Save"],
            AppServices.Loc["Don't Save"],
            AppServices.Loc["Cancel"]);
        return result switch
        {
            ContentDialogResult.Primary => SavePromptResult.Save,
            ContentDialogResult.Secondary => SavePromptResult.DontSave,
            _ => SavePromptResult.Cancel,
        };
    }

    /// <summary>Shows a message with a text box; returns the entered text or null on cancel.</summary>
    public async Task<string?> PromptTextAsync(string title, string message, string prefill = "", bool multiline = false)
    {
        if (Root is null)
        {
            return null;
        }

        var textBox = new TextBox
        {
            Text = prefill,
            AcceptsReturn = multiline,
            Height = multiline ? 120 : double.NaN,
            TextWrapping = TextWrapping.Wrap,
        };
        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(textBox);

        await _gate.WaitAsync();
        try
        {
            var dialog = new ContentDialog
            {
                XamlRoot = Root,
                Title = title,
                Content = panel,
                PrimaryButtonText = AppServices.Loc["OK"],
                CloseButtonText = AppServices.Loc["Cancel"],
                DefaultButton = ContentDialogButton.Primary,
            };
            var result = await dialog.ShowAsync();
            return result == ContentDialogResult.Primary ? textBox.Text : null;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Shows a modal progress dialog and runs the operation with it.</summary>
    public async Task<bool> RunWithProgressAsync(string title, Func<IProgress<double>, CancellationToken, Task> operation)
    {
        if (Root is null)
        {
            return false;
        }

        var bar = new ProgressBar { IsIndeterminate = true, Minimum = 0, Maximum = 1 };
        var statusText = new TextBlock { Text = "", Opacity = 0.7, FontSize = 12 };
        var panel = new StackPanel { Spacing = 12, MinWidth = 320 };
        panel.Children.Add(bar);
        panel.Children.Add(statusText);

        using var cts = new CancellationTokenSource();

        await _gate.WaitAsync();
        try
        {
            var dialog = new ContentDialog
            {
                XamlRoot = Root,
                Title = title,
                Content = panel,
                CloseButtonText = AppServices.Loc["Cancel"],
            };
            dialog.CloseButtonClick += (_, _) => cts.Cancel();

            var progress = new Progress<double>(value =>
            {
                bar.IsIndeterminate = false;
                bar.Value = value;
                statusText.Text = $"{value * 100:0}%";
            });

            var operationTask = RunOperationAsync(operation, progress, cts.Token, dialog);
            _ = dialog.ShowAsync();
            try
            {
                await operationTask;
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            finally
            {
                dialog.Hide();
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private static async Task RunOperationAsync(
        Func<IProgress<double>, CancellationToken, Task> operation,
        IProgress<double> progress,
        CancellationToken ct,
        ContentDialog dialog)
    {
        // Yield first so the dialog is visible before heavy work begins.
        await Task.Yield();
        await operation(progress, ct);
    }

    private async Task<ContentDialogResult> ShowAsync(
        string title, string message, string primary, string? secondary, string? close)
    {
        if (Root is null)
        {
            return ContentDialogResult.None;
        }

        await _gate.WaitAsync();
        try
        {
            var dialog = new ContentDialog
            {
                XamlRoot = Root,
                Title = title,
                Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
                PrimaryButtonText = primary,
                DefaultButton = ContentDialogButton.Primary,
            };
            if (secondary is not null)
            {
                dialog.SecondaryButtonText = secondary;
            }
            if (close is not null)
            {
                dialog.CloseButtonText = close;
            }
            return await dialog.ShowAsync();
        }
        finally
        {
            _gate.Release();
        }
    }
}
