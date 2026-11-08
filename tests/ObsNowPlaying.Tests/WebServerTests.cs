using System.Net;
using System.Net.Sockets;
using System.Text;
using ObsNowPlaying.Media;
using ObsNowPlaying.Settings;
using ObsNowPlaying.Web;
using Xunit;

public class WebServerTests : IDisposable
{
    readonly string _dir = Path.Combine(Path.GetTempPath(), "onp-web-" + Guid.NewGuid().ToString("N"));
    readonly int _port;
    readonly SettingsHost _settings;
    readonly WebServer _server;
    readonly HttpClient _http;
    AlbumArt? _art;

    public WebServerTests()
    {
        var store = new SettingsStore(Path.Combine(_dir, "settings.json"));
        _settings = new SettingsHost(store, store.Load());
        // A free TCP port can still be reserved in http.sys (e.g. WinRM's 5986), so retry a few.
        for (var attempt = 0; ; attempt++)
        {
            _port = FreePort();
            _server = new WebServer(_port, () => NowPlaying.Empty with { Title = "<b>x</b>" }, () => _art, _settings, "file:///C:/w/widget.html");
            try { _server.Start(); break; }
            catch (HttpListenerException) when (attempt < 10) { _server.Dispose(); }
        }
        _http = new HttpClient { BaseAddress = new Uri($"http://localhost:{_port}/") };
    }

    public void Dispose()
    {
        _http.Dispose();
        _server.Dispose();
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
        _art = new AlbumArt([1, 2, 3], "image/png", "1");
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
    public void PortInUse_Throws()
    {
        using var second = new WebServer(_port, () => NowPlaying.Empty, () => null, _settings, "");
        Assert.Throws<HttpListenerException>(second.Start);
    }

    [Fact]
    public async Task Art_StaleVersion_404()
    {
        _art = new AlbumArt([1, 2, 3], "image/png", "NEW");
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
