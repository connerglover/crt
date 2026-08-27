namespace CRT.Core.Files;

/// <summary>
/// Well-known per-user paths. The base directory matches what Python's
/// <c>appdirs.user_config_dir("CRT")</c> resolves to on Windows
/// (<c>%LOCALAPPDATA%\CRT\CRT</c>) so existing users' settings carry over.
/// </summary>
public sealed class ConfigPaths
{
    public ConfigPaths(string? baseDirectory = null)
    {
        BaseDirectory = baseDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CRT", "CRT");
    }

    public string BaseDirectory { get; }

    public string SettingsFile => Path.Combine(BaseDirectory, "settings.ini");

    public string RecentFile => Path.Combine(BaseDirectory, "recent.json");

    public string LibraryFile => Path.Combine(BaseDirectory, "library.json");

    public string AutosaveFile => Path.Combine(BaseDirectory, "autosave.json");

    public string ApiKeyFile => Path.Combine(BaseDirectory, "src_api_key.bin");

    public string ToolsDirectory => Path.Combine(BaseDirectory, "tools");

    public string VideoCacheDirectory => Path.Combine(BaseDirectory, "video-cache");

    public void EnsureBaseDirectory() => Directory.CreateDirectory(BaseDirectory);
}
