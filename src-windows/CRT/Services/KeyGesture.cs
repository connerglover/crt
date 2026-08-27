using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace CRT.Services;

/// <summary>
/// Parses/formats hotkey strings ("Ctrl+Shift+S", ",", "[", "Space") to and
/// from WinUI virtual keys + modifiers. String spellings match the Python/Qt
/// defaults stored in settings.ini so existing user files keep working.
/// </summary>
public static class KeyGesture
{
    private static readonly Dictionary<string, VirtualKey> NamedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Space"] = VirtualKey.Space,
        ["Enter"] = VirtualKey.Enter,
        ["Return"] = VirtualKey.Enter,
        ["Tab"] = VirtualKey.Tab,
        ["Esc"] = VirtualKey.Escape,
        ["Escape"] = VirtualKey.Escape,
        ["Backspace"] = VirtualKey.Back,
        ["Delete"] = VirtualKey.Delete,
        ["Del"] = VirtualKey.Delete,
        ["Insert"] = VirtualKey.Insert,
        ["Home"] = VirtualKey.Home,
        ["End"] = VirtualKey.End,
        ["PgUp"] = VirtualKey.PageUp,
        ["PageUp"] = VirtualKey.PageUp,
        ["PgDown"] = VirtualKey.PageDown,
        ["PageDown"] = VirtualKey.PageDown,
        ["Left"] = VirtualKey.Left,
        ["Right"] = VirtualKey.Right,
        ["Up"] = VirtualKey.Up,
        ["Down"] = VirtualKey.Down,
        [","] = (VirtualKey)0xBC,   // VK_OEM_COMMA
        ["."] = (VirtualKey)0xBE,   // VK_OEM_PERIOD
        [";"] = (VirtualKey)0xBA,   // VK_OEM_1
        ["/"] = (VirtualKey)0xBF,   // VK_OEM_2
        ["`"] = (VirtualKey)0xC0,   // VK_OEM_3
        ["["] = (VirtualKey)0xDB,   // VK_OEM_4
        ["\\"] = (VirtualKey)0xDC,  // VK_OEM_5
        ["]"] = (VirtualKey)0xDD,   // VK_OEM_6
        ["'"] = (VirtualKey)0xDE,   // VK_OEM_7
        ["-"] = (VirtualKey)0xBD,   // VK_OEM_MINUS
        ["="] = (VirtualKey)0xBB,   // VK_OEM_PLUS
    };

    private static readonly Dictionary<VirtualKey, string> KeyNames =
        NamedKeys
            .GroupBy(kv => kv.Value)
            .ToDictionary(g => g.Key, g => g.First().Key);

    /// <summary>Parses "Ctrl+Shift+S" into modifiers + key. Returns false for empty/unknown.</summary>
    public static bool TryParse(string gesture, out VirtualKeyModifiers modifiers, out VirtualKey key)
    {
        modifiers = VirtualKeyModifiers.None;
        key = VirtualKey.None;
        if (string.IsNullOrWhiteSpace(gesture))
        {
            return false;
        }

        string[] parts = gesture.Split('+', StringSplitOptions.TrimEntries);
        // A trailing "+" means the plus key itself ("Ctrl++" style) — treat the
        // final empty part as "=" shifted? Keep simple: drop empties except a
        // literal lone "+".
        var tokens = new List<string>();
        foreach (string part in parts)
        {
            if (part.Length > 0)
            {
                tokens.Add(part);
            }
        }
        if (tokens.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < tokens.Count; i++)
        {
            string token = tokens[i];
            bool isLast = i == tokens.Count - 1;
            switch (token.ToLowerInvariant())
            {
                case "ctrl" or "control":
                    modifiers |= VirtualKeyModifiers.Control;
                    continue;
                case "shift":
                    modifiers |= VirtualKeyModifiers.Shift;
                    continue;
                case "alt":
                    modifiers |= VirtualKeyModifiers.Menu;
                    continue;
                case "win" or "meta":
                    modifiers |= VirtualKeyModifiers.Windows;
                    continue;
            }

            if (!isLast)
            {
                return false; // unknown modifier
            }

            if (NamedKeys.TryGetValue(token, out VirtualKey named))
            {
                key = named;
                return true;
            }
            if (token.Length == 1)
            {
                char c = char.ToUpperInvariant(token[0]);
                if (c is >= 'A' and <= 'Z')
                {
                    key = (VirtualKey)c;
                    return true;
                }
                if (c is >= '0' and <= '9')
                {
                    key = (VirtualKey)c;
                    return true;
                }
            }
            if (token.Length >= 2 && (token[0] is 'F' or 'f') && int.TryParse(token[1..], out int fn) && fn is >= 1 and <= 24)
            {
                key = VirtualKey.F1 + (fn - 1);
                return true;
            }
            return false;
        }
        return false;
    }

    /// <summary>Formats modifiers + key back into the canonical gesture string.</summary>
    public static string Format(VirtualKeyModifiers modifiers, VirtualKey key)
    {
        var parts = new List<string>();
        if (modifiers.HasFlag(VirtualKeyModifiers.Control))
        {
            parts.Add("Ctrl");
        }
        if (modifiers.HasFlag(VirtualKeyModifiers.Shift))
        {
            parts.Add("Shift");
        }
        if (modifiers.HasFlag(VirtualKeyModifiers.Menu))
        {
            parts.Add("Alt");
        }
        if (modifiers.HasFlag(VirtualKeyModifiers.Windows))
        {
            parts.Add("Win");
        }
        parts.Add(KeyName(key));
        return string.Join("+", parts);
    }

    /// <summary>True when the virtual key is only a modifier (no key name of its own).</summary>
    public static bool IsModifierKey(VirtualKey key) => key is
        VirtualKey.Control or VirtualKey.Shift or VirtualKey.Menu or
        VirtualKey.LeftControl or VirtualKey.RightControl or
        VirtualKey.LeftShift or VirtualKey.RightShift or
        VirtualKey.LeftMenu or VirtualKey.RightMenu or
        VirtualKey.LeftWindows or VirtualKey.RightWindows;

    public static string KeyName(VirtualKey key)
    {
        if (KeyNames.TryGetValue(key, out string? name))
        {
            return name;
        }
        int code = (int)key;
        if (code is >= 'A' and <= 'Z' or >= '0' and <= '9')
        {
            return ((char)code).ToString();
        }
        if (key >= VirtualKey.F1 && key <= VirtualKey.F24)
        {
            return $"F{key - VirtualKey.F1 + 1}";
        }
        if (key >= VirtualKey.NumberPad0 && key <= VirtualKey.NumberPad9)
        {
            return $"Num{key - VirtualKey.NumberPad0}";
        }
        return key.ToString();
    }

    /// <summary>
    /// True when <paramref name="key"/> may be given to a
    /// <see cref="KeyboardAccelerator"/>.
    /// </summary>
    /// <remarks>
    /// The OEM keys above (<c>,</c> <c>.</c> <c>[</c> …) are real virtual-key
    /// codes but are not members of <see cref="VirtualKey"/>, and an accelerator
    /// holding one crashes the XAML core natively when its element is attached
    /// to the visual tree. Such gestures must go through <see cref="PageHotkeys"/>,
    /// which dispatches them from a KeyDown handler instead.
    /// </remarks>
    public static bool IsAcceleratorSafe(VirtualKey key) => Enum.IsDefined(key);

    /// <summary>
    /// Builds a KeyboardAccelerator for a gesture string, or null when
    /// unparseable or when the key cannot safely be used as an accelerator.
    /// </summary>
    public static KeyboardAccelerator? CreateAccelerator(string gesture, Action action, bool allowWhenTyping = false)
    {
        if (!TryParse(gesture, out VirtualKeyModifiers modifiers, out VirtualKey key) ||
            !IsAcceleratorSafe(key))
        {
            return null;
        }
        var accelerator = new KeyboardAccelerator { Key = key, Modifiers = modifiers };
        accelerator.Invoked += (_, args) =>
        {
            // Character-producing keys (Space, comma, L, Shift+L, "<", …) must
            // not fire while the user is typing into a text field. Shift alone
            // still produces a character, so only Ctrl/Alt/Win combinations may
            // run there. Returning without handling lets the key through to the
            // focused control.
            if (!allowWhenTyping &&
                (modifiers & ~VirtualKeyModifiers.Shift) == VirtualKeyModifiers.None &&
                IsTextInputFocused(args))
            {
                return;
            }
            args.Handled = true;
            action();
        };
        return accelerator;
    }

    private static bool IsTextInputFocused(KeyboardAcceleratorInvokedEventArgs args)
    {
        var root = (args.Element as Microsoft.UI.Xaml.UIElement)?.XamlRoot;
        object? focused = root is not null
            ? Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(root)
            : null;
        return focused is Microsoft.UI.Xaml.Controls.TextBox
            or Microsoft.UI.Xaml.Controls.PasswordBox
            or Microsoft.UI.Xaml.Controls.AutoSuggestBox
            or Microsoft.UI.Xaml.Controls.RichEditBox;
    }
}
