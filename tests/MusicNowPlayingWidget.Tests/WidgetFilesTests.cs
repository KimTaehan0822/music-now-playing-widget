using MusicNowPlayingWidget.Web;
using Xunit;

public class WidgetFilesTests : IDisposable
{
    readonly string _dir = Path.Combine(Path.GetTempPath(), "onp-wf-" + Guid.NewGuid().ToString("N"), "widget");
    public void Dispose() { var p = Path.GetDirectoryName(_dir)!; if (Directory.Exists(p)) Directory.Delete(p, true); }

    [Fact]
    public void Extract_WritesStandaloneWidget()
    {
        var url = WidgetFiles.Extract(_dir, 9123);
        Assert.StartsWith("file:///", url);
        Assert.EndsWith("/widget.html", url);
        Assert.True(File.Exists(Path.Combine(_dir, "widget.html")));
        Assert.True(File.Exists(Path.Combine(_dir, "widget.js")));
        Assert.Contains("window.ONP_PORT = 9123;", File.ReadAllText(Path.Combine(_dir, "config.js")));
        // file:// pages can't load fonts from sibling files, so the font travels inside the CSS.
        Assert.Contains("url(\"data:font/woff2;base64,", File.ReadAllText(Path.Combine(_dir, "widget.css")));
    }

    [Fact]
    public void Extract_Overwrites()
    {
        WidgetFiles.Extract(_dir, 1111);
        WidgetFiles.Extract(_dir, 2222);
        Assert.Contains("2222", File.ReadAllText(Path.Combine(_dir, "config.js")));
    }

    [Fact]
    public void Build_IsShortHex() => Assert.Matches("^[0-9A-F]{8}$", WidgetFiles.Build);

    [Fact]
    public void Extract_VersionsAssetUrls_SoOldCopiesAreNotReused()
    {
        WidgetFiles.Extract(_dir, 9123);
        var html = File.ReadAllText(Path.Combine(_dir, "widget.html"));
        var v = $"?v={WidgetFiles.Build}-9123";
        Assert.Contains($"widget.css{v}", html);
        Assert.Contains($"config.js{v}", html);
        Assert.Contains($"widget.js{v}", html);
        Assert.Contains($"window.ONP_BUILD = \"{WidgetFiles.Build}\";", File.ReadAllText(Path.Combine(_dir, "config.js")));
    }
}
