using CRT.Core.Files;
using CRT.Core.Localization;
using CRT.Core.Models;
using CRT.Core.Net;
using CRT.Core.Tools;
using Xunit;

namespace CRT.Core.Tests;

public class SessionFileManagerTests : IDisposable
{
    private readonly string _dir;

    public SessionFileManagerTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"crt-files-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private string NewPath(string name) => Path.Combine(_dir, name);

    [Fact]
    public void History_ExcludesActiveFile_NoDuplicates()
    {
        var files = new SessionFileManager();
        string a = NewPath("a.json");
        string b = NewPath("b.json");

        files.SaveAs(a);
        Assert.Empty(files.History());       // active file excluded

        files.SaveAs(b);
        Assert.Equal(new[] { a }, files.History());

        files.LoadFile(a);                   // switching back removes a from history, adds b
        Assert.Equal(new[] { b }, files.History());

        files.NewSession();
        Assert.Contains(a, files.History());
        Assert.Contains(b, files.History());
    }

    [Fact]
    public void SaveWithoutPath_Throws()
    {
        var files = new SessionFileManager();
        Assert.Throws<ValidationException>(() => files.Save());
    }

    [Fact]
    public void DirtyFlag_ClearsOnSaveAndLoad()
    {
        var files = new SessionFileManager();
        files.Session.Mutate(endFrame: 100);
        files.Dirty = true;
        string path = NewPath("dirty.json");
        files.SaveAs(path);
        Assert.False(files.Dirty);

        files.Dirty = true;
        files.LoadFile(path);
        Assert.False(files.Dirty);
        Assert.Equal(100, files.Session.EndFrame);
    }
}

public class RecentFilesTests : IDisposable
{
    private readonly string _dir;

    public RecentFilesTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"crt-recent-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void Touch_BumpsToTop_CapsAt20_Persists()
    {
        string path = Path.Combine(_dir, "recent.json");
        var recent = new RecentFiles(path);
        for (int i = 0; i < 25; i++)
        {
            recent.Touch($@"C:\runs\run{i}.json");
        }
        recent.Touch(@"C:\runs\run3.json");

        Assert.Equal(RecentFiles.Capacity, recent.Paths.Count);
        Assert.Equal(@"C:\runs\run3.json", recent.Paths[0]);

        var reloaded = new RecentFiles(path);
        Assert.Equal(recent.Paths, reloaded.Paths);
    }
}

public class AutosaveServiceTests : IDisposable
{
    private readonly string _dir;

    public AutosaveServiceTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"crt-autosave-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void WriteReadClear_RoundTrip()
    {
        var autosave = new AutosaveService(Path.Combine(_dir, "autosave.json"));
        var session = new TimeSession { StartFrame = 3, EndFrame = 99, Framerate = 29.97m };
        session.AddLoad(10, 20);

        autosave.Write(session, @"C:\runs\wip.json");
        var snapshot = autosave.TryRead();
        Assert.NotNull(snapshot);
        Assert.Equal(3, snapshot!.Session.StartFrame);
        Assert.Equal(99, snapshot.Session.EndFrame);
        Assert.Equal(29.97m, snapshot.Session.Framerate);
        Assert.Single(snapshot.Session.Loads);
        Assert.Equal(@"C:\runs\wip.json", snapshot.FilePath);

        autosave.Clear();
        Assert.Null(autosave.TryRead());
    }
}

public class LocalizationTests
{
    [Fact]
    public void AllFourLanguagesPresent()
    {
        Assert.Equal(new[] { "English", "Français", "Polski", "Español" },
            LanguageCatalog.LanguageNames);
    }

    [Fact]
    public void PythonKeySetPortedToEveryLanguage()
    {
        // Spot-check keys across all four dictionaries.
        Assert.Equal("Taux de refraichissement", LanguageCatalog.Resolve("Français")["Framerate"]);
        Assert.Equal("Liczba klatek na sekundę", LanguageCatalog.Resolve("Polski")["Framerate"]);
        Assert.Equal("Tasa de Fotogramas", LanguageCatalog.Resolve("Español")["Framerate"]);
        Assert.Equal("Skopiuj notatkę moderatora", LanguageCatalog.Resolve("Polski")["Copy Mod Note"]);
        Assert.Equal("Siempre Visible", LanguageCatalog.Resolve("Español")["Always on Top"]);

        // The full Python key set exists in each ported dictionary.
        string[] pythonKeys =
        {
            "Framerate", "Start Frame", "End Frame", "Start Frame (Loads)", "End Frame (Loads)",
            "Paste", "Paste Start Frame", "Paste End Frame", "Paste Start Frame (Loads)",
            "Paste End Frame (Loads)", "Copy Mod Note", "Copy Discord Message",
            "Copy YouTube Chapters", "Add Loads", "Add Load", "Edit Loads", "Without Loads",
            "With Loads", "Click to Copy Time", "File", "New Time", "Open Time",
            "Session History", "Save", "Save As", "Settings", "Exit", "Edit (Menu Bar)",
            "Clear Loads", "View", "Always on Top", "Help", "About", "Edit Load", "Save Edits",
            "Discard Changes", "Edit", "Delete", "Loads", "File Name", "Cancel", "CRT Settings",
            "Automatically Check for Updates", "Theme", "Automatic", "Dark", "Light",
            "Accent Color", "Language", "Mod Note Format", "Restore Defaults", "Apply",
            "Hotkeys", "Customize Hotkeys", "Press a Key Combination", "Reset", "Reset All",
            "OK", "Duplicate Hotkey", "Duplicate Hotkey Message",
        };
        foreach (string language in LanguageCatalog.LanguageNames)
        {
            var content = LanguageCatalog.Resolve(language);
            foreach (string key in pythonKeys)
            {
                Assert.True(content.ContainsKey(key), $"{language} is missing '{key}'");
            }
        }
    }

    [Fact]
    public void UnknownLanguage_FallsBackToEnglish()
    {
        var localizer = new Localizer("en");
        Assert.Equal("Framerate (FPS)", localizer["Framerate"]);
    }

    [Fact]
    public void MissingTranslation_FallsBackToEnglish_ThenKey()
    {
        var localizer = new Localizer("Français");
        Assert.Equal("Coller", localizer["Paste"]);
        // New native-only key exists only in English.
        Assert.Equal("Dashboard", localizer["Dashboard"]);
        // Entirely unknown key returns the key itself.
        Assert.Equal("Totally Unknown Key", localizer["Totally Unknown Key"]);
    }

    [Fact]
    public void Translate_ReverseLookupForThemeNames()
    {
        Assert.Equal("Dark", LanguageCatalog.Translate("Français", "English", "Sombre"));
        Assert.Equal("Automatic", LanguageCatalog.Translate("Español", "English", "Automático"));
        Assert.Equal("NotAThemeName", LanguageCatalog.Translate("Français", "English", "NotAThemeName"));
    }
}

public class YtDlpImporterTests
{
    [Theory]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("https://youtu.be/dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/shorts/dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/watch?list=xx&v=dQw4w9WgXcQ&t=1", "dQw4w9WgXcQ")]
    public void ExtractVideoId_KnownShapes(string url, string expected)
    {
        Assert.Equal(expected, YtDlpImporter.ExtractVideoId(url));
        Assert.True(YtDlpImporter.IsYouTubeUrl(url));
    }

    [Fact]
    public void NonYouTubeUrl_NotDetected()
    {
        Assert.Null(YtDlpImporter.ExtractVideoId("https://example.com/video.mp4"));
        Assert.False(YtDlpImporter.IsYouTubeUrl("https://example.com/video.mp4"));
    }
}

public class FfprobeClientTests
{
    [Theory]
    [InlineData("30000/1001", "29.97")]
    [InlineData("60/1", "60")]
    [InlineData("24000/1001", "23.976")]
    [InlineData("0/0", "0")]
    [InlineData("garbage", "0")]
    public void EvaluateRational(string rational, string expected)
    {
        Assert.Equal(
            decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture),
            FfprobeClient.EvaluateRational(rational));
    }
}

public class UpdateCheckerVersionTests
{
    // The rewrite is 2.0.0 while the newest published tag is the Python app's
    // 1.2.2, so a plain string comparison advertised a downgrade every launch.
    [Fact]
    public void OlderPublishedTagIsNotAnUpdate()
    {
        Assert.False(UpdateChecker.IsNewer("1.2.2", "2.0.0"));
        Assert.False(UpdateChecker.IsNewer("v1.2.2", "2.0.0"));
    }

    [Theory]
    [InlineData("2.0.1", "2.0.0")]
    [InlineData("v2.1.0", "2.0.0")]
    [InlineData("3.0.0", "2.0.0")]
    [InlineData("2.1.0-beta1", "2.0.0")]
    public void NewerTagIsAnUpdate(string latest, string current)
    {
        Assert.True(UpdateChecker.IsNewer(latest, current));
    }

    [Theory]
    [InlineData("2.0.0", "2.0.0")]
    [InlineData("v2.0.0", "2.0.0")]
    public void SameVersionIsNotAnUpdate(string latest, string current)
    {
        Assert.False(UpdateChecker.IsNewer(latest, current));
    }

    [Theory]
    [InlineData("nightly")]
    [InlineData("")]
    [InlineData("release")]
    public void UnparseableTagStaysQuiet(string latest)
    {
        Assert.False(UpdateChecker.IsNewer(latest, "2.0.0"));
    }
}

public class ToolLocatorBundleTests : IDisposable
{
    private readonly string _toolsDir;
    private readonly string _bundledDir;

    public ToolLocatorBundleTests()
    {
        _toolsDir = Path.Combine(Path.GetTempPath(), $"crt-tools-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_toolsDir);

        // The first bundled probe is AppContext.BaseDirectory/tools, which for a
        // test run is the test output folder.
        _bundledDir = ToolLocator.BundledDirectories().First();
        Directory.CreateDirectory(_bundledDir);
    }

    public void Dispose()
    {
        foreach (string path in Directory.EnumerateFiles(_bundledDir, "*.stub"))
        {
            File.Delete(path);
        }
        foreach (string name in new[] { "ffmpeg.exe", "ffprobe.exe", "yt-dlp.exe" })
        {
            string candidate = Path.Combine(_bundledDir, name);
            if (File.Exists(candidate) && new FileInfo(candidate).Length == 0)
            {
                File.Delete(candidate);
            }
        }
        try
        {
            Directory.Delete(_toolsDir, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private ToolLocator Locator(string ffmpegSetting = "", string ytDlpSetting = "") =>
        new(_toolsDir, () => ffmpegSetting, () => ytDlpSetting);

    [Fact]
    public void FindsAToolShippedBesideTheExecutable()
    {
        string shipped = Path.Combine(_bundledDir, "ffmpeg.exe");
        File.WriteAllBytes(shipped, Array.Empty<byte>());

        Assert.Equal(shipped, Locator().Find(ToolKind.Ffmpeg));
    }

    [Fact]
    public void BundledBeatsTheDownloadDirectory()
    {
        // A build that ships the tools should never fall through to the folder
        // downloads land in, or a stale download would win over what shipped.
        string shipped = Path.Combine(_bundledDir, "yt-dlp.exe");
        File.WriteAllBytes(shipped, Array.Empty<byte>());
        File.WriteAllBytes(Path.Combine(_toolsDir, "yt-dlp.exe"), Array.Empty<byte>());

        Assert.Equal(shipped, Locator().Find(ToolKind.YtDlp));
    }

    [Fact]
    public void AnExplicitSettingStillWins()
    {
        string shipped = Path.Combine(_bundledDir, "ffmpeg.exe");
        File.WriteAllBytes(shipped, Array.Empty<byte>());
        string chosen = Path.Combine(_toolsDir, "my-ffmpeg.exe");
        File.WriteAllBytes(chosen, Array.Empty<byte>());

        Assert.Equal(chosen, Locator(ffmpegSetting: chosen).Find(ToolKind.Ffmpeg));
    }

    [Fact]
    public void NothingShippedFallsThroughToTheDownloadDirectory()
    {
        string downloaded = Path.Combine(_toolsDir, "ffprobe.exe");
        File.WriteAllBytes(downloaded, Array.Empty<byte>());

        Assert.Equal(downloaded, Locator().Find(ToolKind.Ffprobe));
    }
}
