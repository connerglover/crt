using CRT.Core.Models;

namespace CRT.Core.Files;

/// <summary>
/// Owns the current session plus its on-disk path, session history and dirty
/// flag — a port of <c>src/crt/file_manager.py</c>.
/// </summary>
public sealed class SessionFileManager
{
    public TimeSession Session { get; private set; } = new();

    public string? FilePath { get; private set; }

    public List<string> PastFilePaths { get; } = new();

    public bool Dirty { get; set; }

    /// <summary>Past file paths, excluding whichever file is currently active.</summary>
    public IReadOnlyList<string> History() =>
        PastFilePaths.Where(p => p != FilePath).ToList();

    private void RememberPastPath(string? path)
    {
        if (!string.IsNullOrEmpty(path) && path != FilePath && !PastFilePaths.Contains(path))
        {
            PastFilePaths.Add(path);
        }
    }

    /// <summary>Loads a run file from disk into the current session.</summary>
    public void LoadFile(string path)
    {
        var loaded = RunFileStore.Load(path);
        string? oldFilePath = FilePath;

        loaded.Precision = Session.Precision;
        Session = loaded;

        FilePath = path;
        Dirty = false;
        PastFilePaths.Remove(path);
        RememberPastPath(oldFilePath);
    }

    /// <summary>Starts a blank session, remembering the previous file in history.</summary>
    public void NewSession(TimingMode defaultMode = TimingMode.Loads)
    {
        string? oldFilePath = FilePath;
        FilePath = null;
        Session = new TimeSession { Mode = defaultMode };
        Dirty = false;
        RememberPastPath(oldFilePath);
    }

    /// <summary>Replaces the current session in place (used by autosave restore).</summary>
    public void ReplaceSession(TimeSession session, string? filePath, bool dirty)
    {
        Session = session;
        FilePath = filePath;
        Dirty = dirty;
    }

    /// <summary>Saves to the current file path. Throws if there isn't one yet.</summary>
    public void Save()
    {
        if (string.IsNullOrEmpty(FilePath))
        {
            throw new ValidationException("No file path set — use Save As first.");
        }
        RunFileStore.Save(Session, FilePath);
        Dirty = false;
    }

    /// <summary>Saves to a new file path, remembering the previous one in history.</summary>
    public void SaveAs(string path)
    {
        string? oldFilePath = FilePath;
        FilePath = path;
        RunFileStore.Save(Session, path);
        Dirty = false;
        RememberPastPath(oldFilePath);
    }
}
