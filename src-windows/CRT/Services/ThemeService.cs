using Microsoft.UI.Xaml;
using Windows.UI;

namespace CRT.Services;

/// <summary>Theme + accent color application (spec §12).</summary>
public static class ThemeService
{
    /// <summary>Maps the stored (English) theme name to an ElementTheme.</summary>
    public static ElementTheme ResolveTheme(string themeName) => themeName switch
    {
        "Dark" => ElementTheme.Dark,
        "Light" => ElementTheme.Light,
        _ => ElementTheme.Default, // Automatic: follow the OS
    };

    /// <summary>
    /// Overrides the system accent color resources so accent-filled controls
    /// (primary buttons, toggles, highlights) pick up the user's color. Must
    /// run before the first window is created. Invalid/empty → system accent.
    /// </summary>
    public static void ApplyAccentColor(string hex)
    {
        if (!TryParseHexColor(hex, out Color accent))
        {
            return; // fall back to system accent
        }

        var resources = Application.Current.Resources;
        resources["SystemAccentColor"] = accent;
        resources["SystemAccentColorLight1"] = Lighten(accent, 0.15);
        resources["SystemAccentColorLight2"] = Lighten(accent, 0.30);
        resources["SystemAccentColorLight3"] = Lighten(accent, 0.45);
        resources["SystemAccentColorDark1"] = Darken(accent, 0.15);
        resources["SystemAccentColorDark2"] = Darken(accent, 0.30);
        resources["SystemAccentColorDark3"] = Darken(accent, 0.45);
    }

    public static bool TryParseHexColor(string hex, out Color color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(hex))
        {
            return false;
        }
        string text = hex.TrimStart('#');
        if (text.Length != 6 ||
            !byte.TryParse(text[..2], System.Globalization.NumberStyles.HexNumber, null, out byte r) ||
            !byte.TryParse(text[2..4], System.Globalization.NumberStyles.HexNumber, null, out byte g) ||
            !byte.TryParse(text[4..6], System.Globalization.NumberStyles.HexNumber, null, out byte b))
        {
            return false;
        }
        color = Color.FromArgb(255, r, g, b);
        return true;
    }

    public static string FormatHexColor(Color color) => $"#{color.R:x2}{color.G:x2}{color.B:x2}";

    private static Color Lighten(Color c, double amount) => Color.FromArgb(255,
        (byte)Math.Clamp(c.R + (255 - c.R) * amount, 0, 255),
        (byte)Math.Clamp(c.G + (255 - c.G) * amount, 0, 255),
        (byte)Math.Clamp(c.B + (255 - c.B) * amount, 0, 255));

    private static Color Darken(Color c, double amount) => Color.FromArgb(255,
        (byte)Math.Clamp(c.R * (1 - amount), 0, 255),
        (byte)Math.Clamp(c.G * (1 - amount), 0, 255),
        (byte)Math.Clamp(c.B * (1 - amount), 0, 255));
}
