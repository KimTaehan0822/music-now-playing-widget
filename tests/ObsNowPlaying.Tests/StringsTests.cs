using System.Globalization;
using ObsNowPlaying.Localization;
using Xunit;

public class StringsTests
{
    static readonly string[] RequiredKeys =
    [
        "tray.openSettings", "tray.copyWidgetUrl", "tray.quit", "tray.urlCopied", "error.portInUse",
        "settings.title", "settings.style", "settings.styleA", "settings.styleB", "settings.styleC",
        "settings.colors", "settings.textColor", "settings.backgroundColor", "settings.accentColor",
        "settings.showArt", "settings.showProgress", "settings.apps", "settings.appsHint", "settings.appsEmpty",
        "settings.language", "settings.languageAuto", "settings.widgetUrls", "settings.copy", "settings.copied",
        "settings.save", "settings.saved", "settings.preview", "settings.nothingPlaying", "settings.showTime", "settings.widgetUrlsHint",
    ];

    [Fact]
    public void Resolve_UsesSetting() =>
        Assert.Equal("ja", Strings.ResolveLanguage("ja", new CultureInfo("en-US")));

    [Theory]
    [InlineData("ko-KR", "ko")]
    [InlineData("ja-JP", "ja")]
    [InlineData("de-DE", "en")]
    public void Resolve_FollowsWindows(string culture, string expected) =>
        Assert.Equal(expected, Strings.ResolveLanguage(null, new CultureInfo(culture)));

    [Fact]
    public void AllLanguages_HaveSameKeys()
    {
        var ko = Strings.Load("ko").Keys.Order().ToArray();
        Assert.Equal(ko, Strings.Load("en").Keys.Order().ToArray());
        Assert.Equal(ko, Strings.Load("ja").Keys.Order().ToArray());
        Assert.All(RequiredKeys, k => Assert.Contains(k, ko));
        Assert.All(Strings.Load("ja").Values, v => Assert.False(string.IsNullOrWhiteSpace(v)));
    }
}
