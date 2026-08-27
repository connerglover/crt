using CRT.Core.Hotkeys;
using CRT.Core.Settings;
using Xunit;

namespace CRT.Core.Tests;

public class IniFileTests
{
    [Fact]
    public void RoundTrip_ConfigParserStyle()
    {
        var ini = new IniFile();
        ini.Set("Settings", "enable_updates", "True");
        ini.Set("Settings", "theme", "Automatic");
        ini.Set("Hotkeys", "new_time", "Ctrl+N");

        // ConfigParser output style: `key = value`, blank line after each section.
        Assert.Equal(
            "[Settings]\nenable_updates = True\ntheme = Automatic\n\n[Hotkeys]\nnew_time = Ctrl+N\n\n",
            ini.ToText());

        var reparsed = IniFile.Parse(ini.ToText());
        Assert.Equal("True", reparsed.Get("Settings", "enable_updates"));
        Assert.Equal("Ctrl+N", reparsed.Get("Hotkeys", "new_time"));
    }

    [Fact]
    public void Parse_AcceptsBothSpacings_AndColonDelimiter()
    {
        var ini = IniFile.Parse("[Settings]\na=1\nb = 2\nc: 3\n");
        Assert.Equal("1", ini.Get("Settings", "a"));
        Assert.Equal("2", ini.Get("Settings", "b"));
        Assert.Equal("3", ini.Get("Settings", "c"));
    }

    [Fact]
    public void Parse_SkipsCommentsAndBlanks()
    {
        var ini = IniFile.Parse("# comment\n; also comment\n\n[S]\nkey = value\n");
        Assert.Equal("value", ini.Get("S", "key"));
    }

    [Fact]
    public void OptionNames_AreCaseInsensitive_LikeConfigParser()
    {
        var ini = IniFile.Parse("[S]\nMyKey = x\n");
        Assert.Equal("x", ini.Get("S", "mykey"));
        Assert.Equal("x", ini.Get("S", "MYKEY"));
    }

    [Fact]
    public void ValueWithEqualsSign_Preserved()
    {
        var ini = IniFile.Parse("[S]\nformat = a=b {x}\n");
        Assert.Equal("a=b {x}", ini.Get("S", "format"));
    }

    [Fact]
    public void GetBoolean_ConfigParserSpellings()
    {
        var ini = IniFile.Parse("[S]\na = True\nb = false\nc = yes\nd = 0\n");
        Assert.True(ini.GetBoolean("S", "a"));
        Assert.False(ini.GetBoolean("S", "b"));
        Assert.True(ini.GetBoolean("S", "c"));
        Assert.False(ini.GetBoolean("S", "d"));
    }
}

public class SettingsServiceTests : IDisposable
{
    private readonly string _dir;

    public SettingsServiceTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"crt-settings-{Guid.NewGuid():N}");
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

    private string IniPath => Path.Combine(_dir, "settings.ini");

    [Fact]
    public void FreshInstall_WritesDefaults()
    {
        var service = new SettingsService(IniPath);
        Assert.True(File.Exists(IniPath));

        var settings = service.Current();
        Assert.True(settings.EnableUpdates);
        Assert.Equal("Automatic", settings.Theme);
        Assert.Equal("#5b9bd5", settings.AccentColor);
        Assert.Equal("en", settings.Language);
        Assert.Equal("Mod Note: Retimed to {time_without_loads}", settings.ModNoteFormat);
        Assert.Equal("bottom-right", settings.TimerCorner);
        Assert.Equal("pill", settings.TimerStyle);
        Assert.Equal("", settings.FfmpegPath);
        Assert.Equal("loads", settings.DefaultMode);
        Assert.Equal("Ctrl+N", settings.Hotkeys["New Time"]);
        Assert.Equal(",", settings.Hotkeys["video_frame_back"]);
    }

    [Fact]
    public void ExistingPythonFile_MissingKeysSynced_ValuesKept()
    {
        // A file the Python app would have written (no native-only keys).
        File.WriteAllText(IniPath,
            "[Settings]\nenable_updates = False\ntheme = Dark\naccent_color = #ff0000\n" +
            "language = Français\nmod_note_format = Retimed: {time_without_loads}\n\n" +
            "[Hotkeys]\nsave = Ctrl+Alt+S\n\n");

        var service = new SettingsService(IniPath);
        var settings = service.Current();

        // Existing values preserved.
        Assert.False(settings.EnableUpdates);
        Assert.Equal("Dark", settings.Theme);
        Assert.Equal("#ff0000", settings.AccentColor);
        Assert.Equal("Français", settings.Language);
        Assert.Equal("Ctrl+Alt+S", settings.Hotkeys["Save"]);

        // Missing keys synced with defaults and rewritten to disk.
        Assert.Equal("bottom-right", settings.TimerCorner);
        string text = File.ReadAllText(IniPath);
        Assert.Contains("timer_corner = bottom-right", text);
        Assert.Contains("toggle_mode = Ctrl+T", text);
        Assert.Contains("enable_updates = False", text);
    }

    [Fact]
    public void Apply_RoundTrips()
    {
        var service = new SettingsService(IniPath);
        var settings = service.Current();
        settings.Theme = "Light";
        settings.AccentColor = "#123456";
        settings.Hotkeys["Save"] = "Ctrl+Shift+F12";
        service.Apply(settings);

        var reloaded = new SettingsService(IniPath).Current();
        Assert.Equal("Light", reloaded.Theme);
        Assert.Equal("#123456", reloaded.AccentColor);
        Assert.Equal("Ctrl+Shift+F12", reloaded.Hotkeys["Save"]);
    }

    [Fact]
    public void RestoreDefaults_ResetsEverything()
    {
        var service = new SettingsService(IniPath);
        var settings = service.Current();
        settings.Theme = "Dark";
        service.Apply(settings);

        service.RestoreDefaults();
        Assert.Equal("Automatic", service.Current().Theme);
    }
}

public class HotkeyRegistryTests
{
    [Theory]
    [InlineData("Copy Mod Note", "copy_mod_note")]
    [InlineData("Settings", "settings")]
    [InlineData("Save As", "save_as")]
    [InlineData("start_paste", "start_paste")]
    [InlineData("Paste Start Frame (Loads)", "paste_start_frame_loads")]
    [InlineData("Toggle Mode", "toggle_mode")]
    [InlineData("video_frame_back", "video_frame_back")]
    public void SlugRule(string actionId, string expected)
    {
        Assert.Equal(expected, HotkeyRegistry.OptionName(actionId));
    }

    [Fact]
    public void Defaults_MatchPythonPlusNewActions()
    {
        Assert.Equal("Ctrl+N", HotkeyRegistry.Defaults["New Time"]);
        Assert.Equal("Ctrl+O", HotkeyRegistry.Defaults["Open Time"]);
        Assert.Equal("Ctrl+H", HotkeyRegistry.Defaults["Session History"]);
        Assert.Equal("Ctrl+S", HotkeyRegistry.Defaults["Save"]);
        Assert.Equal("Ctrl+Shift+S", HotkeyRegistry.Defaults["Save As"]);
        Assert.Equal("Ctrl+,", HotkeyRegistry.Defaults["Settings"]);
        Assert.Equal("Ctrl+M", HotkeyRegistry.Defaults["Copy Mod Note"]);
        Assert.Equal("Ctrl+Shift+D", HotkeyRegistry.Defaults["Copy Discord Message"]);
        Assert.Equal("Ctrl+Shift+Y", HotkeyRegistry.Defaults["Copy YouTube Chapters"]);
        Assert.Equal("Ctrl+Shift+L", HotkeyRegistry.Defaults["Clear Loads"]);
        Assert.Equal("Ctrl+1", HotkeyRegistry.Defaults["start_paste"]);
        Assert.Equal("Ctrl+2", HotkeyRegistry.Defaults["end_paste"]);
        Assert.Equal("Ctrl+3", HotkeyRegistry.Defaults["start_loads_paste"]);
        Assert.Equal("Ctrl+4", HotkeyRegistry.Defaults["end_loads_paste"]);
        Assert.Equal("Ctrl+L", HotkeyRegistry.Defaults["Add Loads"]);
        Assert.Equal(",", HotkeyRegistry.Defaults["video_frame_back"]);
        Assert.Equal(".", HotkeyRegistry.Defaults["video_frame_forward"]);
        Assert.Equal("Space", HotkeyRegistry.Defaults["video_play_pause"]);
        Assert.Equal("[", HotkeyRegistry.Defaults["video_mark_start"]);
        Assert.Equal("]", HotkeyRegistry.Defaults["video_mark_end"]);
        Assert.Equal("L", HotkeyRegistry.Defaults["video_mark_load_start"]);
        Assert.Equal("Shift+L", HotkeyRegistry.Defaults["video_mark_load_end"]);
        Assert.Equal("Ctrl+T", HotkeyRegistry.Defaults["Toggle Mode"]);
    }

    [Fact]
    public void FindDuplicates_GroupsBySequence()
    {
        var duplicates = HotkeyRegistry.FindDuplicates(new Dictionary<string, string>
        {
            ["Save"] = "Ctrl+M",
            ["Copy Mod Note"] = "Ctrl+M",
            ["Open Time"] = "Ctrl+O",
        });
        var group = Assert.Single(duplicates);
        Assert.Contains("Save", group);
        Assert.Contains("Copy Mod Note", group);
    }
}
