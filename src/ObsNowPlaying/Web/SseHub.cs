using System.Net;
using System.Text;
using Timer = System.Threading.Timer;

namespace ObsNowPlaying.Web;

public sealed class SseHub : IDisposable
{
    readonly List<HttpListenerResponse> _clients = [];
    readonly Timer _ping;

    public SseHub() => _ping = new Timer(_ => SendRaw(": ping\n\n"), null, 15000, 15000);

    public void Add(HttpListenerResponse response, IEnumerable<string> initialMessages)
    {
        response.ContentType = "text/event-stream";
        response.Headers["Cache-Control"] = "no-cache";
        response.SendChunked = true;
        lock (_clients)
        {
            foreach (var m in initialMessages) if (!TryWrite(response, m)) return;
            _clients.Add(response);
        }
    }

    public static string Format(string evt, string json) => $"event: {evt}\ndata: {json}\n\n";

    public void Send(string evt, string json) => SendRaw(Format(evt, json));

    void SendRaw(string message)
    {
        lock (_clients) _clients.RemoveAll(c => !TryWrite(c, message));
    }

    static bool TryWrite(HttpListenerResponse r, string message)
    {
        try
        {
            r.OutputStream.Write(Encoding.UTF8.GetBytes(message));
            r.OutputStream.Flush();
            return true;
        }
        catch
        {
            try { r.Abort(); } catch { }
            return false; // the browser went away; drop it
        }
    }

    public void Dispose()
    {
        _ping.Dispose();
        lock (_clients)
        {
            foreach (var c in _clients) try { c.Abort(); } catch { }
            _clients.Clear();
        }
    }
}
