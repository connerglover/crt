using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using Windows.UI.Core;

namespace CRT.Services;

/// <summary>
/// Installs a page's hotkeys, routing each gesture to whichever of the two
/// available mechanisms can actually carry it.
/// </summary>
/// <remarks>
/// <para>
/// Most gestures become a <see cref="KeyboardAccelerator"/>. OEM keys cannot:
/// <c>,</c> <c>.</c> <c>[</c> <c>]</c> and friends are valid virtual-key codes
/// (VK_OEM_COMMA = 0xBC …) but are <em>not</em> members of the
/// <see cref="VirtualKey"/> enum, and giving one to an accelerator terminates
/// the process the moment the owning element is attached to the visual tree.
/// The failure is a native stowed exception (0xC000027B) raised inside the XAML
/// core, so <c>Application.UnhandledException</c> never sees it and the app
/// simply vanishes — which is what made the Video Retimer page unopenable, since
/// its frame-step and mark defaults are exactly <c>, . [ ]</c>.
/// </para>
/// <para>
/// Ctrl/Alt/Win combinations survive (<c>Ctrl+,</c> on the Frame Retimer has
/// always worked), but bare and Shift-only ones do not. Rather than encode that
/// empirical boundary, every OEM key is dispatched from a <c>KeyDown</c> handler
/// instead — one rule, no reliance on where exactly the crash line sits.
/// </para>
/// <para>
/// The handler is attached to the window root rather than the page so that these
/// gestures keep working when focus sits outside the page content (the
/// navigation pane, for instance), matching accelerator reach. It is attached on
/// <c>Loaded</c> and removed on <c>Unloaded</c>, so a cached page that has been
/// navigated away from stops responding, as it should.
/// </para>
/// </remarks>
public sealed class PageHotkeys
{
    private readonly record struct Fallback(
        VirtualKey Key,
        VirtualKeyModifiers Modifiers,
        Action Action,
        bool AllowWhenTyping);

    private readonly FrameworkElement _owner;
    private readonly List<Fallback> _fallbacks = new();
    private readonly KeyEventHandler _handler;
    private readonly RoutedEventHandler _onLoaded;
    private readonly RoutedEventHandler _onUnloaded;
    private UIElement? _attachedTo;

    public PageHotkeys(FrameworkElement owner)
    {
        _owner = owner;
        _handler = OnKeyDown;
        _onLoaded = (_, _) => Attach();
        _onUnloaded = (_, _) => Detach();

        // These accelerators belong to the page as a whole, not to any one
        // control. Left on the default Auto, WinUI advertises the owner's first
        // accelerator as an automatic tooltip on every descendant that has none
        // of its own — so hovering the time cards showed "Ctrl + N", the New Time
        // binding, purely because it was registered first.
        owner.KeyboardAcceleratorPlacementMode = KeyboardAcceleratorPlacementMode.Hidden;

        owner.Loaded += _onLoaded;
        owner.Unloaded += _onUnloaded;
    }

    /// <summary>
    /// Tears this binding set down completely, so a page can rebuild its
    /// hotkeys after the user edits them.
    /// </summary>
    /// <remarks>
    /// Both the window-root key handler and the owner's Loaded/Unloaded
    /// subscriptions have to go: without this a rebuild would stack a second
    /// live handler on top of the first, and every edit would add another.
    /// </remarks>
    public void Dispose()
    {
        Detach();
        _fallbacks.Clear();
        _owner.Loaded -= _onLoaded;
        _owner.Unloaded -= _onUnloaded;
        _owner.KeyboardAccelerators.Clear();
    }

    /// <summary>
    /// Binds <paramref name="gesture"/> to <paramref name="action"/>. Unparseable
    /// gestures are ignored, matching the previous behavior for unknown keys.
    /// </summary>
    public void Bind(string gesture, Action action, bool allowWhenTyping = false)
    {
        if (!KeyGesture.TryParse(gesture, out VirtualKeyModifiers modifiers, out VirtualKey key))
        {
            return;
        }

        if (KeyGesture.IsAcceleratorSafe(key))
        {
            if (KeyGesture.CreateAccelerator(gesture, action, allowWhenTyping) is { } accelerator)
            {
                _owner.KeyboardAccelerators.Add(accelerator);
            }
            return;
        }

        _fallbacks.Add(new Fallback(key, modifiers, action, allowWhenTyping));
    }

    /// <summary>Binds a gesture looked up from the user's hotkey table.</summary>
    public void Bind(IReadOnlyDictionary<string, string> hotkeys, string actionId, Action action, bool allowWhenTyping = false)
    {
        if (hotkeys.TryGetValue(actionId, out string? gesture))
        {
            Bind(gesture, action, allowWhenTyping);
        }
    }

    private void Attach()
    {
        if (_fallbacks.Count == 0 || _attachedTo is not null)
        {
            return;
        }
        _attachedTo = _owner.XamlRoot?.Content as UIElement;
        _attachedTo?.AddHandler(UIElement.KeyDownEvent, _handler, false);
    }

    private void Detach()
    {
        _attachedTo?.RemoveHandler(UIElement.KeyDownEvent, _handler);
        _attachedTo = null;
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Handled)
        {
            return;
        }

        VirtualKeyModifiers modifiers = CurrentModifiers();
        foreach (Fallback fallback in _fallbacks)
        {
            if (fallback.Key != e.Key || fallback.Modifiers != modifiers)
            {
                continue;
            }

            // Same rule as KeyGesture.CreateAccelerator: a character-producing
            // gesture must not fire while the user is typing into a field.
            if (!fallback.AllowWhenTyping &&
                (modifiers & ~VirtualKeyModifiers.Shift) == VirtualKeyModifiers.None &&
                IsTextInputFocused())
            {
                return;
            }

            e.Handled = true;
            fallback.Action();
            return;
        }
    }

    private static VirtualKeyModifiers CurrentModifiers()
    {
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
        if (IsDown(VirtualKey.LeftWindows) || IsDown(VirtualKey.RightWindows))
        {
            modifiers |= VirtualKeyModifiers.Windows;
        }
        return modifiers;
    }

    private static bool IsDown(VirtualKey key) =>
        InputKeyboardSource.GetKeyStateForCurrentThread(key).HasFlag(CoreVirtualKeyStates.Down);

    private bool IsTextInputFocused()
    {
        if (_owner.XamlRoot is not { } root)
        {
            return false;
        }
        return FocusManager.GetFocusedElement(root) is TextBox or PasswordBox or AutoSuggestBox or RichEditBox;
    }
}
