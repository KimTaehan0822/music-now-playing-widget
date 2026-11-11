using MusicNowPlayingWidget.Settings;
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

    const string MelonId = "com.kakaoent.melon.player";

    [Fact]
    public void ProgramName_Matches_IgnoresCaseSpacesAndDots()
    {
        Assert.True(ProgramName.Matches("Melon Player", MelonId));
        Assert.True(ProgramName.Matches("Spotify", "Spotify.exe"));
        Assert.False(ProgramName.Matches("foobar2000", "Spotify.exe"));
        Assert.False(ProgramName.Matches("...", "anything"));
    }

    [Fact]
    public void Sanitize_FixesBadProgramValues()
    {
        var s = new AppSettings { Programs = new() { ["foobar2000"] = new ProgramEntry { Mode = "x", Order = "y" } } }.Sanitize();
        Assert.Equal(ProgramEntry.Windows, s.Programs["FOOBAR2000"].Mode);
        Assert.Equal(ProgramEntry.TitleArtist, s.Programs["foobar2000"].Order);
        Assert.Empty(new AppSettings { Programs = null! }.Sanitize().Programs);
    }

    [Fact]
    public void RememberApp_MergesPreselectedWindowsRow()
    {
        var s = new AppSettings();
        s.Programs["Melon Player"] = new ProgramEntry { Enabled = false, Mode = ProgramEntry.Windows };
        Assert.True(s.RememberApp(MelonId));
        Assert.False(s.Apps[MelonId]);
        Assert.False(s.Programs.ContainsKey("Melon Player"));
    }

    [Fact]
    public void RememberApp_KeepsTitleRows()
    {
        var s = new AppSettings();
        s.Programs["Melon Player"] = new ProgramEntry { Enabled = false, Mode = ProgramEntry.Title };
        s.RememberApp(MelonId);
        Assert.True(s.Apps[MelonId]);
        Assert.True(s.Programs.ContainsKey("Melon Player"));
    }

    [Fact]
    public void Sanitize_DropsWindowsRowForAppAlreadyListed()
    {
        // Saved from a page that still showed the row after the server merged it, or picked for a listed app.
        var s = new AppSettings();
        s.Apps[MelonId] = false;
        s.Programs["Melon Player"] = new ProgramEntry { Enabled = true, Mode = ProgramEntry.Windows };
        s.Programs["foobar2000"] = new ProgramEntry { Mode = ProgramEntry.Windows };
        s.Sanitize();
        Assert.False(s.Programs.ContainsKey("Melon Player"));
        Assert.True(s.Programs.ContainsKey("foobar2000"));
        Assert.False(s.Apps[MelonId]);
    }

    [Fact]
    public void Sanitize_KeepsTitleRowForAppAlreadyListed()
    {
        var s = new AppSettings();
        s.Apps[MelonId] = true;
        s.Programs["Melon Player"] = new ProgramEntry { Mode = ProgramEntry.Title };
        Assert.True(s.Sanitize().Programs.ContainsKey("Melon Player"));
    }

    [Fact]
    public void IsAppEnabled_UsesPreselectedRowBeforeSeen()
    {
        var s = new AppSettings();
        s.Programs["Melon Player"] = new ProgramEntry { Enabled = false, Mode = ProgramEntry.Windows };
        Assert.False(s.IsAppEnabled(MelonId));
    }
}
