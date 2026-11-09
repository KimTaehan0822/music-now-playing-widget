using System.Net;
using System.Net.Sockets;
using System.Text;
using MusicNowPlayingWidget.Media;
using MusicNowPlayingWidget.Settings;
using MusicNowPlayingWidget.Web;
using Xunit;

public class WebServerTests : IDisposable
{
    readonly string _dir = Path.Combine(Path.GetTempPath(), "onp-web-" + Guid.NewGuid().ToString("N"));
    readonly int _port;
    readonly SettingsHost _settings;
    readonly WebServer _server;
    readonly HttpClient _http;
    readonly string _runKey = @"Software\MusicNowPlayingWidgetTests" + Guid.NewGuid().ToString("N");
    AlbumArt? _art;
    NowPlaying _now = NowPlaying.Empty with { Title = "<b>x</b>" };

    public WebServerTests()
    {
        var store = new SettingsStore(Path.Combine(_dir, "settings.json"));
        _settings = new SettingsHost(store, store.Load());
        // A free TCP port can still be reserved in http.sys (e.g. WinRM's 5986), so retry a few.
        for (var attempt = 0; ; attempt++)
        {
            _port = FreePort();
            _server = new WebServer(_port, () => _now, () => _art, _settings, "file:///C:/w/widget.html",
                new StartupEntry(_runKey, "MusicNowPlayingWidget", @"C:\w\w.exe"));
            try { _server.Start(); break; }
            catch (HttpListenerException) when (attempt < 10) { _server.Dispose(); }
        }
        _http = new HttpClient { BaseAddress = new Uri($"http://localhost:{_port}/") };
    }

    public void Dispose()
    {
        _http.Dispose();
        _server.Dispose();
        Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(_runKey, throwOnMissingSubKey: false);
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    static int FreePort()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var p = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return p;
    }

    [Fact]
    public async Task Widget_ServesHtml()
    {
        var r = await _http.GetAsync("widget?style=B");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal("text/html", r.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task I18n_ServesJson()
    {
        var r = await _http.GetAsync("i18n/ja.json");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Contains("設定", await r.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task UnknownPath_404() =>
        Assert.Equal(HttpStatusCode.NotFound, (await _http.GetAsync("nope.html")).StatusCode);

    [Fact]
    public async Task Art_None_404() =>
        Assert.Equal(HttpStatusCode.NotFound, (await _http.GetAsync("art?v=1")).StatusCode);

    [Fact]
    public async Task Art_ServesImage()
    {
        _art = new AlbumArt([1, 2, 3], "image/png", "1", null);
        var r = await _http.GetAsync("art?v=1");
        Assert.Equal("image/png", r.Content.Headers.ContentType?.MediaType);
        Assert.Equal(new byte[] { 1, 2, 3 }, await r.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Settings_PostMergesSanitizesAndSaves()
    {
        _settings.Remember("chrome.exe");
        var r = await _http.PostAsync("api/settings",
            new StringContent("{\"defaultStyle\":\"Z\",\"accentColor\":\"#112233\"}", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var s = _settings.Get();
        Assert.Equal("A", s.DefaultStyle);
        Assert.Equal("#112233", s.AccentColor);
        Assert.True(s.Apps.ContainsKey("chrome.exe")); // a partial post keeps the other fields
        Assert.True(File.Exists(Path.Combine(_dir, "settings.json")));
        Assert.Contains("\"accentColor\"", await _http.GetStringAsync("api/settings"));
    }

    [Fact]
    public async Task Settings_PostFromOtherSite_Forbidden()
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "api/settings")
        { Content = new StringContent("{\"defaultStyle\":\"C\"}", Encoding.UTF8, "application/json") };
        req.Headers.Add("Origin", "https://evil.example");
        Assert.Equal(HttpStatusCode.Forbidden, (await _http.SendAsync(req)).StatusCode);
        Assert.Equal("A", _settings.Get().DefaultStyle);
    }

    Task<HttpResponseMessage> PostStartup(string body, string? origin = null)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "api/startup")
        { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        if (origin is not null) req.Headers.Add("Origin", origin);
        return _http.SendAsync(req);
    }

    [Fact]
    public async Task Startup_DefaultsOff() =>
        Assert.Equal("{\"enabled\":false}", await _http.GetStringAsync("api/startup"));

    [Fact]
    public async Task Startup_PostOn_ThenGetOn()
    {
        var r = await PostStartup("{\"enabled\":true}");
        Assert.Equal("{\"enabled\":true}", await r.Content.ReadAsStringAsync());
        Assert.Equal("{\"enabled\":true}", await _http.GetStringAsync("api/startup"));
    }

    [Fact]
    public async Task Startup_RejectsForeignOrigin()
    {
        Assert.Equal(HttpStatusCode.Forbidden, (await PostStartup("{\"enabled\":true}", "https://evil.example")).StatusCode);
        Assert.Equal("{\"enabled\":false}", await _http.GetStringAsync("api/startup"));
    }

    [Fact]
    public async Task Startup_BadJson_400()
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await PostStartup("not json")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostStartup("{\"enabled\":\"yes\"}")).StatusCode);
    }

    [Fact]
    public async Task Startup_NotInSettingsJson()
    {
        await PostStartup("{\"enabled\":true}");
        var json = await _http.GetStringAsync("api/settings");
        Assert.DoesNotContain("startup", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("startWithWindows", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Apps_ListsSeenApps()
    {
        _settings.Remember("Spotify.exe");
        _settings.Remember("chrome.exe");
        var json = await _http.GetStringAsync("api/apps");
        Assert.Contains("\"appId\":\"Spotify.exe\",\"enabled\":true", json);
        Assert.Contains("\"appId\":\"chrome.exe\",\"enabled\":false", json);
    }

    [Fact]
    public async Task Events_SendsInitialState()
    {
        using var r = await _http.GetAsync("events", HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal("text/event-stream", r.Content.Headers.ContentType?.MediaType);
        using var reader = new StreamReader(await r.Content.ReadAsStreamAsync());
        var text = new StringBuilder();
        using var cts = new CancellationTokenSource(3000);
        while (!(text.ToString().Contains("event: settings") && text.ToString().Contains("\"title\"")))
            text.AppendLine(await reader.ReadLineAsync(cts.Token));
        Assert.Contains("u003Cb", text.ToString()); // HTML in titles is JSON-escaped, never raw markup
        Assert.DoesNotContain("<b>", text.ToString());
    }

    [Fact]
    public async Task Events_NowPlayingCarriesPalette()
    {
        _now = _now with { Title = "t", Palette = new ArtPalette("#112233", "#FF8800", "#FFFFFF") };
        using var r = await _http.GetAsync("events", HttpCompletionOption.ResponseHeadersRead);
        using var reader = new StreamReader(await r.Content.ReadAsStreamAsync());
        var text = new StringBuilder();
        using var cts = new CancellationTokenSource(3000);
        while (!text.ToString().Contains("\"title\""))
            text.AppendLine(await reader.ReadLineAsync(cts.Token));
        Assert.Contains("\"palette\":{\"background\":\"#112233\",\"accent\":\"#FF8800\",\"text\":\"#FFFFFF\"}", text.ToString());
    }

    [Fact]
    public async Task Events_SendBuild_SoOldWidgetsReload()
    {
        using var r = await _http.GetAsync("events", HttpCompletionOption.ResponseHeadersRead);
        using var reader = new StreamReader(await r.Content.ReadAsStreamAsync());
        var text = new StringBuilder();
        using var cts = new CancellationTokenSource(3000);
        while (!text.ToString().Contains("event: build"))
            text.AppendLine(await reader.ReadLineAsync(cts.Token));
        text.AppendLine(await reader.ReadLineAsync(cts.Token));
        Assert.Contains($"data: \"{WidgetFiles.Build}\"", text.ToString());
    }

    [Fact]
    public void PortInUse_Throws()
    {
        using var second = new WebServer(_port, () => NowPlaying.Empty, () => null, _settings, "");
        Assert.Throws<HttpListenerException>(second.Start);
    }

    [Fact]
    public async Task Art_StaleVersion_404()
    {
        _art = new AlbumArt([1, 2, 3], "image/png", "NEW", null);
        Assert.Equal(HttpStatusCode.NotFound, (await _http.GetAsync("art?v=OLD")).StatusCode);
    }

    [Fact]
    public async Task ConfigJs_CarriesPort() =>
        Assert.Contains($"window.ONP_PORT = {_port};", await _http.GetStringAsync("config.js"));

    [Fact]
    public async Task Info_GivesWidgetFileUrl() =>
        Assert.Contains("file:///C:/w/widget.html", await _http.GetStringAsync("api/info"));

    [Theory]
    [InlineData("null", "null")]               // a widget opened as file:///
    [InlineData("http://absolute", "http://absolute")] // OBS "Local file" mode
    [InlineData("https://evil.example", null)]
    public async Task Events_CorsOnlyForLocalFiles(string origin, string? expected)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, "events");
        req.Headers.Add("Origin", origin);
        using var r = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
        r.Headers.TryGetValues("Access-Control-Allow-Origin", out var values);
        Assert.Equal(expected, values?.Single());
    }
}
