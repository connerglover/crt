using System.Text.Json;

namespace CRT.Core.Files;

/// <summary>Persisted recent-files list (<c>recent.json</c>), capped at 20 entries.</summary>
public sealed class RecentFiles
{
    public const int Capacity = 20;

    private readonly string _path;
    private readonly List<string> _paths = new();

    public RecentFiles(string path)
    {
        _path = path;
        LoadFromDisk();
    }

    public IReadOnlyList<string> Paths => _paths;

    /// <summary>Adds (or bumps) a path to the top of the list and persists.</summary>
    public void Touch(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }
        _paths.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        _paths.Insert(0, path);
        while (_paths.Count > Capacity)
        {
            _paths.RemoveAt(_paths.Count - 1);
        }
        SaveToDisk();
    }

    public void Remove(string path)
    {
        if (_paths.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase)) > 0)
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
            var parsed = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(_path));
            if (parsed is not null)
            {
                _paths.AddRange(parsed.Where(p => !string.IsNullOrWhiteSpace(p)).Take(Capacity));
            }
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            // A corrupt or unreadable recent list is not worth interrupting startup for.
        }
    }

    private void SaveToDisk()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(_paths));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Best-effort persistence.
        }
    }
}
