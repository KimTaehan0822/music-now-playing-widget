using MusicNowPlayingWidget.Media;
using Xunit;

public class WindowTitleTests
{
    [Fact]
    public void Parse_TitleArtist() =>
        Assert.Equal(("Finest Thing", "RESCENE (리센느)"), WindowTitle.Parse("Finest Thing - RESCENE (리센느)", "Melon", false));

    [Fact]
    public void Parse_ArtistTitle() =>
        Assert.Equal(("Dead Fresh", "Lil Baby"), WindowTitle.Parse("Lil Baby - Dead Fresh", "x", true));

    [Fact]
    public void Parse_SplitsAtLastDash_ForTitleFirst() =>
        Assert.Equal(("Song - Remastered", "Artist"), WindowTitle.Parse("Song - Remastered - Artist", "x", false));

    [Fact]
    public void Parse_SplitsAtFirstDash_ForArtistFirst() =>
        Assert.Equal(("Song - Live", "Artist"), WindowTitle.Parse("Artist - Song - Live", "x", true));

    [Fact]
    public void Parse_StripsTrailingProgramName() =>
        Assert.Equal(("Song", "Artist"), WindowTitle.Parse("Song - Artist - foobar2000", "foobar2000", false));

    [Fact]
    public void Parse_StripsShortProgramName() =>
        Assert.Equal(("Song", "Artist"), WindowTitle.Parse("Song - Artist - Melon", "Melon Player", false));

    [Fact]
    public void Parse_ShortProgramNameOnly_IsNull() =>
        Assert.Null(WindowTitle.Parse("Melon", "Melon Player", false));

    [Fact]
    public void Parse_NoSeparator_IsWholeTitle() =>
        Assert.Equal(("Just a title", ""), WindowTitle.Parse("Just a title", "x", false));

    [Theory]
    [InlineData("TIDAL", "TIDAL")]
    [InlineData("", "x")]
    [InlineData(null, "x")]
    [InlineData("  ", "x")]
    public void Parse_EmptyOrProgramNameOnly_IsNull(string? title, string program) =>
        Assert.Null(WindowTitle.Parse(title, program, false));

    static readonly DateTimeOffset T0 = new(2026, 11, 11, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Silence_StaysPlayingWithinGrace()
    {
        var t = new SilenceTimer(TimeSpan.FromSeconds(3));
        Assert.True(t.IsPlaying(true, true, T0));
        Assert.True(t.IsPlaying(false, true, T0.AddSeconds(2)));
        Assert.False(t.IsPlaying(false, true, T0.AddSeconds(3)));
    }

    [Fact]
    public void Silence_NeverHeard_IsNotPlaying() =>
        Assert.False(new SilenceTimer(TimeSpan.FromSeconds(3)).IsPlaying(false, true, T0));

    [Fact]
    public void Silence_UnknownAudio_FollowsTitle()
    {
        var t = new SilenceTimer(TimeSpan.FromSeconds(3));
        Assert.True(t.IsPlaying(null, true, T0));
        Assert.False(t.IsPlaying(null, false, T0));
    }
}
