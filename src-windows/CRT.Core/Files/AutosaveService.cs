using System.Text.Json;
using CRT.Core.Models;

namespace CRT.Core.Files;

/// <summary>An autosave snapshot restored after a crash.</summary>
public sealed record AutosaveSnapshot(TimeSession Session, string? FilePath, DateTimeOffset SavedAt);

/// <summary>
/// Crash-recovery autosave: dirty sessions snapshot to <c>autosave.json</c>
/// every 30 seconds; the file is deleted on clean exit so its presence on
/// launch means the previous session crashed with unsaved work.
/// </summary>
public sealed class AutosaveService
{
    public const int IntervalSeconds = 30;

    private readonly string _path;

    public AutosaveService(string path)
    {
        _path = path;
    }

    public void Write(TimeSession session, string? filePath)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var payload = new Dictionary<string, object?>
            {
                ["file_path"] = filePath,
                ["saved_at"] = DateTimeOffset.Now.ToString("O"),
                ["run"] = JsonSerializer.Deserialize<JsonElement>(RunFileStore.Serialize(session)),
            };
            File.WriteAllText(_path, JsonSerializer.Serialize(payload));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            // Autosave is best-effort; never interrupt the user's session for it.
        }
    }

    /// <summary>Reads a pending snapshot, or null when none exists / it is unreadable.</summary>
    public AutosaveSnapshot? TryRead()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return null;
            }
            using var document = JsonDocument.Parse(File.ReadAllText(_path));
            var root = document.RootElement;

            string? filePath = root.TryGetProperty("file_path", out JsonElement pathElement) && pathElement.ValueKind == JsonValueKind.String
                ? pathElement.GetString()
                : null;
            DateTimeOffset savedAt = root.TryGetProperty("saved_at", out JsonElement savedElement) && savedElement.ValueKind == JsonValueKind.String
                && DateTimeOffset.TryParse(savedElement.GetString(), out DateTimeOffset parsed)
                ? parsed
                : DateTimeOffset.Now;

            var session = RunFileStore.Deserialize(root.GetProperty("run").GetRawText());
            return new AutosaveSnapshot(session, filePath, savedAt);
        }
        catch (Exception e) when (e is JsonException or ValidationException or IOException or UnauthorizedAccessException or KeyNotFoundException)
        {
            return null;
        }
    }

    /// <summary>Deletes any pending snapshot (clean exit or declined restore).</summary>
    public void Clear()
    {
        try
        {
            if (File.Exists(_path))
            {
                File.Delete(_path);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Best-effort.
        }
    }
}
