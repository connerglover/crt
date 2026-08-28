using Microsoft.Win32;

namespace CRT.Core.Tools;

/// <summary>One installed font family and the files that render it.</summary>
public sealed record InstalledFont(string Family, string RegularFile, string BoldFile);

/// <summary>
/// Enumerates the fonts installed on the machine, so the timer is not limited to
/// a hand-written list.
/// </summary>
/// <remarks>
/// <para>
/// ffmpeg's drawtext needs a font <em>file</em>, not a family name, so the
/// mapping has to be resolved here. Windows records it in the registry: the
/// machine-wide fonts key plus the per-user one, which is where anything the
/// user installed without administrator rights lands.
/// </para>
/// <para>
/// Registry names look like "Consolas (TrueType)" or "Arial Bold (TrueType)", so
/// the family is the name with the suffix and any weight words removed, and the
/// bold face is matched back to its family by that same name.
/// </para>
/// </remarks>
public static class SystemFontIndex
{
    private const string FontsKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts";

    /// <summary>
    /// Only "Bold" folds into its family. "Black", "Heavy" and "Semibold" name
    /// separate families — treating them as weights made Arial's bold face
    /// resolve to Arial Black, which is a different typeface, and hid those
    /// families from the picker entirely.
    /// </summary>
    private static readonly string[] BoldMarkers = { " bold" };

    private static readonly string[] SkippedMarkers =
    {
        " italic", " oblique", " light", " thin", " condensed", " narrow", " medium",
    };

    private static IReadOnlyList<InstalledFont>? _cache;

    /// <summary>
    /// Installed families in alphabetical order, cached for the session.
    /// </summary>
    /// <remarks>
    /// Falls back to the fonts that ship with Windows if the registry cannot be
    /// read, so the picker is never empty.
    /// </remarks>
    public static IReadOnlyList<InstalledFont> Families => _cache ??= Build();

    /// <summary>Family names only, for the picker.</summary>
    public static IReadOnlyList<string> FamilyNames =>
        Families.Select(f => f.Family).ToList();

    /// <summary>Resolves a family and weight to a font file, or null if unknown.</summary>
    public static string? FindFile(string family, bool bold)
    {
        var match = Families.FirstOrDefault(
            f => string.Equals(f.Family, family, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            return null;
        }
        string chosen = bold && match.BoldFile.Length > 0 ? match.BoldFile : match.RegularFile;
        return chosen.Length > 0 && File.Exists(chosen) ? chosen : null;
    }

    private static IReadOnlyList<InstalledFont> Build()
    {
        // CRT.Core targets a portable framework so it can be shared with a macOS
        // build later. The font registry is Windows-only, and the check has to
        // guard the Registry roots themselves, not just their use.
        if (!OperatingSystem.IsWindows())
        {
            return Fallback();
        }

        var regular = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var bold = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        Read(Registry.LocalMachine, Environment.GetFolderPath(Environment.SpecialFolder.Fonts));
        Read(Registry.CurrentUser, Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "Windows", "Fonts"));

        var families = regular.Keys
            .Select(family => new InstalledFont(
                family,
                regular[family],
                bold.TryGetValue(family, out string? boldFile) ? boldFile : ""))
            .OrderBy(f => f.Family, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return families.Count > 0 ? families : Fallback();

        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        void Read(RegistryKey root, string defaultDirectory)
        {
            try
            {
                using RegistryKey? key = root.OpenSubKey(FontsKey);
                if (key is null)
                {
                    return;
                }
                foreach (string name in key.GetValueNames())
                {
                    if (key.GetValue(name) is not string file || file.Length == 0)
                    {
                        continue;
                    }
                    // Only outline formats; drawtext cannot use .fon bitmaps.
                    string extension = Path.GetExtension(file);
                    if (!extension.Equals(".ttf", StringComparison.OrdinalIgnoreCase) &&
                        !extension.Equals(".otf", StringComparison.OrdinalIgnoreCase) &&
                        !extension.Equals(".ttc", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string path = Path.IsPathRooted(file) ? file : Path.Combine(defaultDirectory, file);
                    if (!File.Exists(path))
                    {
                        continue;
                    }

                    string label = StripSuffix(name);
                    string lowered = " " + label.ToLowerInvariant();
                    if (SkippedMarkers.Any(marker => lowered.Contains(marker, StringComparison.Ordinal)))
                    {
                        continue;
                    }

                    string? boldMarker = BoldMarkers.FirstOrDefault(
                        marker => lowered.EndsWith(marker, StringComparison.Ordinal));
                    if (boldMarker is not null)
                    {
                        string family = label[..^boldMarker.Length].Trim();
                        if (family.Length > 0)
                        {
                            bold.TryAdd(family, path);
                        }
                        continue;
                    }

                    if (label.Length > 0)
                    {
                        regular.TryAdd(label, path);
                    }
                }
            }
            catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException)
            {
                // An unreadable hive just means fewer fonts, not a failure.
            }
        }
    }

    /// <summary>Trims the "(TrueType)" style suffix the registry appends.</summary>
    private static string StripSuffix(string name)
    {
        int paren = name.IndexOf(" (", StringComparison.Ordinal);
        string trimmed = paren >= 0 ? name[..paren] : name;

        // A single entry can name several families ("Arial,Arial Narrow").
        int comma = trimmed.IndexOf(',');
        return (comma >= 0 ? trimmed[..comma] : trimmed).Trim();
    }

    private static IReadOnlyList<InstalledFont> Fallback()
    {
        string directory = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        return new List<InstalledFont>
        {
            new("Consolas", Path.Combine(directory, "consola.ttf"), Path.Combine(directory, "consolab.ttf")),
            new("Arial", Path.Combine(directory, "arial.ttf"), Path.Combine(directory, "arialbd.ttf")),
            new("Segoe UI", Path.Combine(directory, "segoeui.ttf"), Path.Combine(directory, "segoeuib.ttf")),
        };
    }
}
