using ObsNowPlaying.Settings;
using Xunit;

public class AppSettingsTests
{
    [Theory]
    [InlineData("Chrome")]
    [InlineData("msedge.exe")]
    [InlineData("Microsoft.MicrosoftEdge.Stable_8wekyb3d8bbwe!App")]
    [InlineData(@"C:\Program Files\Naver\Naver Whale\Application\whale.exe")]
    [InlineData("308046B0AF4A39CB")] // Firefox AUMID
    public void Browser_DefaultsOff(string app) => Assert.False(new AppSettings().IsAppEnabled(app));

    [Theory]
    [InlineData("Spotify.exe")]
    [InlineData("SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify")]
    [InlineData("AppleInc.AppleMusicWin_nzyj5cx40ttqa!App")]
    [InlineData("Melon.exe")]
    public void MusicApp_DefaultsOn(string app) => Assert.True(new AppSettings().IsAppEnabled(app));

    [Fact]
    public void ExplicitChoice_Wins()
    {
        var s = new AppSettings();
        s.Apps["chrome.exe"] = true;
        s.Apps["Spotify.exe"] = false;
        Assert.True(s.IsAppEnabled("CHROME.EXE"));
        Assert.False(s.IsAppEnabled("Spotify.exe"));
    }

    [Fact]
    public void RememberApp_AddsOnce()
    {
        var s = new AppSettings();
        Assert.True(s.RememberApp("chrome.exe"));
        Assert.False(s.RememberApp("chrome.exe"));
        Assert.False(s.Apps["chrome.exe"]);
    }

    [Fact]
    public void Sanitize_FixesBadValues()
    {
        var s = new AppSettings { DefaultStyle = "Z", TextColor = "red", BackgroundColor = "#12345G", AccentColor = "#abcdef", Language = "fr", Port = 80 }.Sanitize();
        Assert.Equal("A", s.DefaultStyle);
        Assert.Equal("#FFFFFF", s.TextColor);
        Assert.Equal("#14161C", s.BackgroundColor);
        Assert.Equal("#abcdef", s.AccentColor);
        Assert.Null(s.Language);
        Assert.Equal(8976, s.Port);
    }
}
