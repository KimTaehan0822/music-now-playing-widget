using MusicNowPlayingWidget.Settings;
using MusicNowPlayingWidget.Web;
using Xunit;

public class SettingsHostTests : IDisposable
{
    readonly string _dir = Path.Combine(Path.GetTempPath(), "onp-host-" + Guid.NewGuid().ToString("N"));
    public void Dispose() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

    [Fact]
    public void Remember_NeverMutatesPublishedSettings()
    {
        var store = new SettingsStore(Path.Combine(_dir, "settings.json"));
        var host = new SettingsHost(store, store.Load());
        var published = host.Get(); // another thread may be serializing this right now
        Assert.True(host.Remember("Spotify.exe"));
        Assert.Empty(published.Apps);
        Assert.True(host.Get().Apps.ContainsKey("Spotify.exe"));
    }

    [Fact]
    public void Programs_RoundTripThroughStore()
    {
        var path = Path.Combine(_dir, "settings.json");
        var store = new SettingsStore(path);
        var host = new SettingsHost(store, store.Load());
        host.Merge("{\"programs\":{\"foobar2000\":{\"enabled\":true,\"mode\":\"title\",\"order\":\"artist-title\"}}}");
        var loaded = new SettingsStore(path).Load().Programs["foobar2000"];
        Assert.Equal("artist-title", loaded.Order);
        Assert.Equal("title", loaded.Mode);
    }

    [Fact]
    public void OldSettingsFile_LoadsEmptyPrograms()
    {
        var path = Path.Combine(_dir, "settings.json");
        Directory.CreateDirectory(_dir);
        File.WriteAllText(path, "{\"port\":8976}");
        var programs = new SettingsStore(path).Load().Programs;
        Assert.NotNull(programs);
        Assert.Empty(programs);
    }
}
