using System.Net;
using System.Text;
using System.Threading.Channels;
using Timer = System.Threading.Timer;

namespace MusicNowPlayingWidget.Web;

/// Server-sent events. Each browser has its own queue and writer, so one stalled reader never blocks the others.
public sealed class SseHub : IDisposable
{
    const int MaxQueued = 64; // a reader this far behind is stuck; drop it

    sealed class Client(HttpListenerResponse response)
    {
        public readonly Channel<string> Queue = Channel.CreateBounded<string>(MaxQueued);
        public readonly HttpListenerResponse Response = response;
    }

    readonly List<Client> _clients = [];
    readonly Timer _ping;

    public SseHub() => _ping = new Timer(_ => SendRaw(": ping\n\n"), null, 15000, 15000);

    /// The initial messages are built under the same lock as broadcasts, so nothing sent in between is missed.
    public void Add(HttpListenerResponse response, Func<IEnumerable<string>> initialMessages)
    {
        response.ContentType = "text/event-stream";
        response.Headers["Cache-Control"] = "no-cache";
        response.SendChunked = true;
        var client = new Client(response);
        lock (_clients)
        {
            foreach (var m in initialMessages()) client.Queue.Writer.TryWrite(m);
            _clients.Add(client);
        }
        _ = Task.Run(() => Pump(client));
    }

    public static string Format(string evt, string json) => $"event: {evt}\ndata: {json}\n\n";

    public void Send(string evt, string json) => SendRaw(Format(evt, json));

    void SendRaw(string message)
    {
        lock (_clients)
            foreach (var c in _clients.ToList())
                if (!c.Queue.Writer.TryWrite(message)) Drop(c);
    }

    async Task Pump(Client c)
    {
        try
        {
            await foreach (var message in c.Queue.Reader.ReadAllAsync())
            {
                await c.Response.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(message));
                await c.Response.OutputStream.FlushAsync();
            }
        }
        catch { } // the browser went away
        lock (_clients) Drop(c);
    }

    void Drop(Client c)
    {
        _clients.Remove(c);
        c.Queue.Writer.TryComplete();
        try { c.Response.Abort(); } catch { }
    }

    public void Dispose()
    {
        _ping.Dispose();
        lock (_clients) foreach (var c in _clients.ToList()) Drop(c);
    }
}
