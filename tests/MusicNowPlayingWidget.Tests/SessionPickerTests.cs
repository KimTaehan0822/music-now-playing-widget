using MusicNowPlayingWidget.Media;
using Xunit;

public class SessionPickerTests
{
    static readonly DateTimeOffset T0 = new(2026, 11, 9, 0, 0, 0, TimeSpan.Zero);

    static SessionInfo S(string app, bool playing, int startedSec = 0) =>
        new(app, app + " song", "artist", "", playing, T0.AddSeconds(startedSec), null, null, T0);

    static bool AllButChrome(string app) => app != "chrome.exe";

    [Fact]
    public void Empty_ReturnsNull() =>
        Assert.Null(SessionPicker.Pick([], null, AllButChrome));

    [Fact]
    public void DisabledApp_IsIgnored() =>
        Assert.Null(SessionPicker.Pick([S("chrome.exe", true)], "chrome.exe", AllButChrome));

    [Fact]
    public void OnlyPlaying_IsChosen() =>
        Assert.Equal("Melon.exe", SessionPicker.Pick([S("Spotify.exe", false), S("Melon.exe", true)], "Spotify.exe", AllButChrome)?.AppId);

    [Fact]
    public void CurrentSession_WinsAmongPlaying() =>
        Assert.Equal("Spotify.exe", SessionPicker.Pick([S("Melon.exe", true, 10), S("Spotify.exe", true, 0)], "Spotify.exe", AllButChrome)?.AppId);

    [Fact]
    public void MostRecentStart_WinsWhenCurrentNotEligible() =>
        Assert.Equal("Melon.exe", SessionPicker.Pick(
            [S("Spotify.exe", true, 0), S("Melon.exe", true, 10), S("chrome.exe", true, 20)], "chrome.exe", AllButChrome)?.AppId);

    [Fact]
    public void NothingPlaying_ReturnsNull() =>
        Assert.Null(SessionPicker.Pick([S("Spotify.exe", false), S("Melon.exe", false)], "Spotify.exe", AllButChrome));
}
