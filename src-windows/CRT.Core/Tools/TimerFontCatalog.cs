using System.Globalization;

namespace CRT.Core.Tools;

/// <summary>
/// Maps a font family + weight onto a file drawtext can load.
/// </summary>
/// <remarks>
/// ffmpeg's drawtext takes a font <em>file</em>, not a family name, so the
/// picker offers families that ship with Windows and this resolves each to its
/// regular or bold file. Anything missing falls back to Consolas, which is
/// monospaced — a proportional font makes a running clock jitter as digits
/// change width, so it stays the default.
/// </remarks>
public static class TimerFontCatalog
{
    private const string FontsDirectory = @"C:\Windows\Fonts";

    private static readonly Dictionary<string, (string Regular, string Bold)> Files =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Consolas"] = ("consola.ttf", "consolab.ttf"),
            ["Courier New"] = ("cour.ttf", "courbd.ttf"),
            ["Lucida Console"] = ("lucon.ttf", "lucon.ttf"),
            ["Segoe UI"] = ("segoeui.ttf", "segoeuib.ttf"),
            ["Arial"] = ("arial.ttf", "arialbd.ttf"),
            ["Verdana"] = ("verdana.ttf", "verdanab.ttf"),
            ["Tahoma"] = ("tahoma.ttf", "tahomabd.ttf"),
            ["Trebuchet MS"] = ("trebuc.ttf", "trebucbd.ttf"),
            ["Georgia"] = ("georgia.ttf", "georgiab.ttf"),
            ["Times New Roman"] = ("times.ttf", "timesbd.ttf"),
            ["Impact"] = ("impact.ttf", "impact.ttf"),
        };

    public const string DefaultFamily = "Consolas";

    /// <summary>
    /// Every font installed on the machine, not just the curated list.
    /// </summary>
    /// <remarks>
    /// The curated table below is still the fallback path: it maps the fonts
    /// that ship with Windows to known filenames, which covers the case where
    /// the registry cannot be read.
    /// </remarks>
    public static IReadOnlyList<string> Families => SystemFontIndex.FamilyNames;

    /// <summary>
    /// Resolves a family and weight to an absolute font path, falling back to
    /// the default family and then to whatever Consolas resolves to.
    /// </summary>
    public static string ResolveFile(string family, bool bold)
    {
        // Anything installed, resolved through the registry index first.
        string? installed = SystemFontIndex.FindFile(family ?? "", bold);
        if (installed is not null)
        {
            return Normalize(installed);
        }

        if (!Files.TryGetValue(family ?? "", out var entry))
        {
            entry = Files[DefaultFamily];
        }

        string path = Path.Combine(FontsDirectory, bold ? entry.Bold : entry.Regular);
        if (File.Exists(path))
        {
            return Normalize(path);
        }

        // Bold variants are the ones most likely to be absent on a trimmed
        // install; the regular face of the same family is a closer substitute
        // than a different family.
        string regular = Path.Combine(FontsDirectory, entry.Regular);
        if (File.Exists(regular))
        {
            return Normalize(regular);
        }
        return Normalize(Path.Combine(FontsDirectory, Files[DefaultFamily].Regular));
    }

    /// <summary>
    /// drawtext wants forward slashes; a Windows path reaches it with the drive
    /// colon escaped and its backslashes intact, which the filter parser reads
    /// as escapes of its own.
    /// </summary>
    private static string Normalize(string path) => path.Replace('\\', '/');

    /// <summary>
    /// Splits a colour into decimal components, for filters that take channels
    /// separately rather than a colour string — <c>geq</c> in particular.
    /// </summary>
    public static (int R, int G, int B) Rgb(string hex, string fallback)
    {
        string text = (hex ?? "").TrimStart('#');
        if (text.Length != 6 || !text.All(Uri.IsHexDigit))
        {
            text = fallback;
        }
        return (
            Convert.ToInt32(text[..2], 16),
            Convert.ToInt32(text[2..4], 16),
            Convert.ToInt32(text[4..6], 16));
    }

    /// <summary>
    /// Converts <c>#rrggbb</c> to the <c>0xRRGGBB</c> form drawtext expects,
    /// optionally with an alpha suffix. Invalid input falls back to
    /// <paramref name="fallback"/> rather than producing a filter ffmpeg
    /// rejects at the very end of a long export.
    /// </summary>
    public static string Color(string hex, string fallback, int opacityPercent = 100)
    {
        string text = (hex ?? "").TrimStart('#');
        bool valid = text.Length == 6 &&
                     text.All(c => Uri.IsHexDigit(c));
        string rgb = valid ? text.ToUpperInvariant() : fallback;

        if (opacityPercent >= 100)
        {
            return "0x" + rgb;
        }
        double alpha = Math.Clamp(opacityPercent, 0, 100) / 100.0;
        return "0x" + rgb + "@" + alpha.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
