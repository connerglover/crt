using System.Text;

namespace CRT.Core.Settings;

/// <summary>
/// Minimal INI reader/writer compatible with Python's ConfigParser output:
/// <c>[Section]</c> headers, <c>key = value</c> lines (reads <c>key=value</c>
/// and <c>key: value</c> too), <c>#</c>/<c>;</c> comments, option names
/// lower-cased, a blank line after each section.
/// </summary>
public sealed class IniFile
{
    private readonly List<string> _sectionOrder = new();
    private readonly Dictionary<string, Dictionary<string, string>> _sections =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<string>> _optionOrder =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> Sections => _sectionOrder;

    public static IniFile Parse(string text)
    {
        var ini = new IniFile();
        string? currentSection = null;

        foreach (string rawLine in text.Replace("\r\n", "\n").Split('\n'))
        {
            string line = rawLine.TrimEnd();
            string trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#') || trimmed.StartsWith(';'))
            {
                continue;
            }

            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                currentSection = trimmed[1..^1].Trim();
                ini.EnsureSection(currentSection);
                continue;
            }

            if (currentSection is null)
            {
                continue;
            }

            int eq = trimmed.IndexOf('=');
            int colon = trimmed.IndexOf(':');
            int split = eq >= 0 && (colon < 0 || eq < colon) ? eq : colon;
            if (split <= 0)
            {
                continue;
            }

            string key = trimmed[..split].Trim();
            string value = trimmed[(split + 1)..].Trim();
            ini.Set(currentSection, key, value);
        }

        return ini;
    }

    public static IniFile Load(string path) => Parse(File.ReadAllText(path));

    public bool HasSection(string section) => _sections.ContainsKey(section);

    public bool HasOption(string section, string option) =>
        _sections.TryGetValue(section, out var options) && options.ContainsKey(Normalize(option));

    public void EnsureSection(string section)
    {
        if (_sections.ContainsKey(section))
        {
            return;
        }
        _sections[section] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _optionOrder[section] = new List<string>();
        _sectionOrder.Add(section);
    }

    public void Set(string section, string option, string value)
    {
        EnsureSection(section);
        string key = Normalize(option);
        if (!_sections[section].ContainsKey(key))
        {
            _optionOrder[section].Add(key);
        }
        _sections[section][key] = value;
    }

    public string? Get(string section, string option) =>
        _sections.TryGetValue(section, out var options) && options.TryGetValue(Normalize(option), out string? value)
            ? value
            : null;

    public string Get(string section, string option, string fallback) =>
        Get(section, option) ?? fallback;

    public bool GetBoolean(string section, string option, bool fallback = false)
    {
        string? value = Get(section, option);
        if (value is null)
        {
            return fallback;
        }
        // ConfigParser accepts these spellings for booleans.
        return value.ToLowerInvariant() switch
        {
            "1" or "yes" or "true" or "on" => true,
            "0" or "no" or "false" or "off" => false,
            _ => fallback,
        };
    }

    public IReadOnlyList<string> Options(string section) =>
        _optionOrder.TryGetValue(section, out var order) ? order : Array.Empty<string>();

    /// <summary>Serializes in ConfigParser's output style.</summary>
    public string ToText()
    {
        var sb = new StringBuilder();
        foreach (string section in _sectionOrder)
        {
            sb.Append('[').Append(section).Append("]\n");
            foreach (string option in _optionOrder[section])
            {
                sb.Append(option).Append(" = ").Append(_sections[section][option]).Append('\n');
            }
            sb.Append('\n');
        }
        return sb.ToString();
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, ToText());
    }

    /// <summary>ConfigParser lower-cases option names (optionxform).</summary>
    private static string Normalize(string option) => option.ToLowerInvariant();
}
