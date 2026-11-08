using System.Net;
using System.Text;
using System.Text.Json;
using MusicNowPlayingWidget.Media;
using MusicNowPlayingWidget.Settings;

namespace MusicNowPlayingWidget.Web;

public sealed class WebServer(int port, Func<NowPlaying> current, Func<AlbumArt?> art, SettingsHost settings, string widgetFileUrl) : IDisposable
{
    static readonly Dictionary<string, string> Types = new()
    {
        [".html"] = "text/html; charset=utf-8", [".css"] = "text/css; charset=utf-8",
        [".js"] = "text/javascript; charset=utf-8", [".json"] = "application/json; charset=utf-8",
        [".woff2"] = "font/woff2", [".ico"] = "image/x-icon", [".svg"] = "image/svg+xml",
    };

    readonly HttpListener _listener = new();
    readonly SseHub _hub = new();

    public int Port => port;

    public void Start()
    {
        _listener.Prefixes.Add($"http://localhost:{port}/");
        _listener.Start(); // throws HttpListenerException when the port is taken
        _ = Task.Run(Loop);
    }

    public void Broadcast(string evt, object data) => _hub.Send(evt, Json(data));

    static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web); // compact: one SSE data line

    static string Json(object data) => JsonSerializer.Serialize(data, Wire);

    async Task Loop()
    {
        while (_listener.IsListening)
        {
            HttpListenerContext ctx;
            try { ctx = await _listener.GetContextAsync(); }
            catch { return; } // listener stopped
            _ = Task.Run(() => Handle(ctx));
        }
    }

    void Handle(HttpListenerContext ctx)
    {
        var req = ctx.Request;
        var res = ctx.Response;
        try
        {
            var path = req.Url!.AbsolutePath;
            switch (req.HttpMethod, path)
            {
                case ("GET", "/"):
                    res.Redirect("/settings");
                    break;
                case ("GET", "/widget"):
                    Resource(res, "widget.html");
                    break;
                case ("GET", "/settings"):
                    Resource(res, "settings.html");
                    break;
                case ("GET", "/events"):
                    // Widgets opened as a local file send Origin "null" (file:///) or "http://absolute" (OBS "Local file").
                    if (req.Headers["Origin"] is "null" or "http://absolute") res.Headers["Access-Control-Allow-Origin"] = req.Headers["Origin"];
                    _hub.Add(res, () => [SseHub.Format("settings", Json(settings.Get())), SseHub.Format("nowplaying", Json(current())),
                        SseHub.Format("build", Json(WidgetFiles.Build))]);
                    return; // stays open
                case ("GET", "/config.js"):
                    res.ContentType = "text/javascript; charset=utf-8";
                    res.OutputStream.Write(Encoding.UTF8.GetBytes(WidgetFiles.ConfigJs(port)));
                    break;
                case ("GET", "/api/info"):
                    Text(res, Json(new { widgetUrl = widgetFileUrl }));
                    break;
                case ("GET", "/art"):
                    var a = art();
                    // Only the current cover: an old ?v= must never get (and cache) a newer image.
                    if (a is null || req.QueryString["v"] != a.Version) { res.StatusCode = 404; break; }
                    res.ContentType = a.ContentType;
                    res.Headers["Cache-Control"] = "max-age=86400"; // URL carries ?v=<hash>
                    res.OutputStream.Write(a.Bytes);
                    break;
                case ("GET", "/api/settings"):
                    Text(res, Json(settings.Get()));
                    break;
                case ("GET", "/api/apps"):
                    Text(res, Json(settings.Apps()));
                    break;
                case ("POST", "/api/settings"):
                    if (!IsLocalOrigin(req.Headers["Origin"])) { res.StatusCode = 403; break; }
                    try
                    {
                        using var reader = new StreamReader(req.InputStream, Encoding.UTF8);
                        settings.Merge(reader.ReadToEnd());
                    }
                    catch (JsonException) { res.StatusCode = 400; break; }
                    Text(res, Json(settings.Get()));
                    break;
                case ("GET", _):
                    Resource(res, path.TrimStart('/'));
                    break;
                default:
                    res.StatusCode = 405;
                    break;
            }
            res.Close();
        }
        catch
        {
            try { res.Abort(); } catch { }
        }
    }

    // Blocks other websites open in the streamer's browser from rewriting settings.
    static bool IsLocalOrigin(string? origin) =>
        origin is null || (Uri.TryCreate(origin, UriKind.Absolute, out var u) && u.IsLoopback);

    static void Text(HttpListenerResponse res, string json)
    {
        res.ContentType = "application/json; charset=utf-8";
        res.OutputStream.Write(Encoding.UTF8.GetBytes(json));
    }

    static void Resource(HttpListenerResponse res, string name)
    {
        using var stream = typeof(WebServer).Assembly.GetManifestResourceStream("wwwroot/" + name);
        if (stream is null) { res.StatusCode = 404; return; }
        res.ContentType = Types.GetValueOrDefault(Path.GetExtension(name), "application/octet-stream");
        res.Headers["Cache-Control"] = "no-cache";
        stream.CopyTo(res.OutputStream);
    }

    public void Dispose()
    {
        _hub.Dispose();
        if (_listener.IsListening) _listener.Stop();
        _listener.Close();
    }
}
