using MusicNowPlayingWidget.Settings;
using Xunit;

public class SettingsStoreTests : IDisposable
{
    readonly string _dir = Path.Combine(Path.GetTempPath(), "onp-test-" + Guid.NewGuid().ToString("N"));
    string FilePath => Path.Combine(_dir, "settings.json");
    public void Dispose() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

    [Fact]
    public void Load_MissingFile_ReturnsDefaults() =>
        Assert.Equal("A", new SettingsStore(FilePath).Load().DefaultStyle);

    [Fact]
    public void Load_CorruptJson_ReturnsDefaults()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, "{oops");
        Assert.Equal(8976, new SettingsStore(FilePath).Load().Port);
    }

    [Fact]
    public void Load_BadValues_AreSanitized()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, "{\"defaultStyle\":\"Q\",\"apps\":null}");
        var s = new SettingsStore(FilePath).Load();
        Assert.Equal("A", s.DefaultStyle);
        Assert.False(s.IsAppEnabled("chrome.exe"));
    }

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        var store = new SettingsStore(FilePath);
        var s = new AppSettings { DefaultStyle = "C", Language = "ja", ShowArt = false };
        s.Apps["chrome.exe"] = true;
        store.Save(s);
        var back = store.Load();
        Assert.Equal("C", back.DefaultStyle);
        Assert.Equal("ja", back.Language);
        Assert.False(back.ShowArt);
        Assert.True(back.IsAppEnabled("Chrome.exe"));
        Assert.Contains("\"defaultStyle\"", File.ReadAllText(FilePath));
    }

    [Fact]
    public void Load_OneBadField_KeepsTheRest()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, "{\"port\":\"abc\",\"accentColor\":\"#112233\",\"showTime\":false,\"apps\":{\"Melon.exe\":false}}");
        var s = new SettingsStore(FilePath).Load();
        Assert.Equal(8976, s.Port);
        Assert.Equal("#112233", s.AccentColor);
        Assert.False(s.ShowTime);
        Assert.False(s.IsAppEnabled("Melon.exe"));
    }

    [Fact]
    public void ShowTime_DefaultsOn() => Assert.True(new SettingsStore(FilePath).Load().ShowTime);

    [Fact]
    public void AutoColors_DefaultsOff_AndRoundTrips()
    {
        var store = new SettingsStore(FilePath);
        var s = store.Load();
        Assert.False(s.AutoColors);
        s.AutoColors = true;
        store.Save(s);
        Assert.True(new SettingsStore(FilePath).Load().AutoColors);
    }

    [Fact]
    public void Migrate_CopiesOldSettings_WhenNewOnesAreMissing()
    {
        var old = Path.Combine(_dir, "old", "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(old)!);
        File.WriteAllText(old, "{\"accentColor\":\"#112233\"}");
        Assert.True(SettingsStore.Migrate(old, FilePath));
        Assert.Equal("#112233", new SettingsStore(FilePath).Load().AccentColor);
        Assert.True(File.Exists(old)); // the old copy stays, so going back to an old version still works
    }

    [Fact]
    public void Migrate_KeepsNewSettings()
    {
        var old = Path.Combine(_dir, "old", "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(old)!);
        File.WriteAllText(old, "{\"accentColor\":\"#112233\"}");
        File.WriteAllText(FilePath, "{\"accentColor\":\"#445566\"}");
        Assert.False(SettingsStore.Migrate(old, FilePath));
        Assert.Equal("#445566", new SettingsStore(FilePath).Load().AccentColor);
    }

    [Fact]
    public void Migrate_NothingToCopy() => Assert.False(SettingsStore.Migrate(Path.Combine(_dir, "none.json"), FilePath));
}
