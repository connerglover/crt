using System.Text.Json;
using System.Text.Json.Serialization;
using CRT.Core.Formatting;
using CRT.Core.Models;

namespace CRT.Core.Files;

/// <summary>A single run known to the dashboard library.</summary>
public sealed class RunLibraryEntry
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = "";

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("game")]
    public string Game { get; set; } = "";

    [JsonPropertyName("mode")]
    public string Mode { get; set; } = "loads";

    /// <summary>ISO display string for the primary time (without loads / segment total).</summary>
    [JsonPropertyName("time_without_loads")]
    public string TimeWithoutLoads { get; set; } = "";

    /// <summary>ISO display string for the secondary time (with loads / full run).</summary>
    [JsonPropertyName("time_with_loads")]
    public string TimeWithLoads { get; set; } = "";

    [JsonPropertyName("modified")]
    public string Modified { get; set; } = "";

    [JsonIgnore]
    public string DisplayTitle =>
        !string.IsNullOrWhiteSpace(Title) ? Title : System.IO.Path.GetFileNameWithoutExtension(Path);
}

/// <summary>The dashboard run library index (<c>library.json</c>).</summary>
public sealed class RunLibrary
{
    private readonly string _path;
    private readonly List<RunLibraryEntry> _entries = new();

    public RunLibrary(string path)
    {
        _path = path;
        LoadFromDisk();
    }

    /// <summary>Entries, most recently modified first.</summary>
    public IReadOnlyList<RunLibraryEntry> Entries =>
        _entries.OrderByDescending(e => e.Modified, StringComparer.Ordinal).ToList();

    /// <summary>Records (or refreshes) a run file that was saved or opened by the app.</summary>
    public void Upsert(TimeSession session, string filePath)
    {
        var entry = _entries.FirstOrDefault(e =>
            string.Equals(e.Path, filePath, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            entry = new RunLibraryEntry { Path = filePath };
            _entries.Add(entry);
        }

        entry.Title = session.Meta.Title;
        entry.Game = session.Meta.Game;
        entry.Mode = session.Mode.ToSerialString();
        entry.TimeWithoutLoads = TimeFormatter.FormatIso(session.PrimarySeconds);
        entry.TimeWithLoads = TimeFormatter.FormatIso(session.SecondarySeconds);
        entry.Modified = !string.IsNullOrEmpty(session.Meta.Modified)
            ? session.Meta.Modified
            : DateTimeOffset.Now.ToString("yyyy-MM-dd'T'HH:mm:sszzz");
        SaveToDisk();
    }

    /// <summary>Removes an entry from the library (does not delete the file).</summary>
    public void Remove(string filePath)
    {
        if (_entries.RemoveAll(e => string.Equals(e.Path, filePath, StringComparison.OrdinalIgnoreCase)) > 0)
        {
            SaveToDisk();
        }
    }

    private void LoadFromDisk()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return;
            }
            var parsed = JsonSerializer.Deserialize<List<RunLibraryEntry>>(File.ReadAllText(_path));
            if (parsed is not null)
            {
                _entries.AddRange(parsed.Where(e => !string.IsNullOrWhiteSpace(e.Path)));
            }
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            // A corrupt library index rebuilds itself as runs are saved.
        }
    }

    private void SaveToDisk()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(_entries, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Best-effort persistence.
        }
    }
}
