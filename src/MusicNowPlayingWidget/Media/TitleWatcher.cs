using System.Diagnostics;
using MusicNowPlayingWidget.Settings;
using Timer = System.Threading.Timer;

namespace MusicNowPlayingWidget.Media;

/// Reads the song from the window titles of programs the user set to "window title".
/// Runs once a second only while such a program is turned on; otherwise does nothing.
public sealed class TitleWatcher : IDisposable
{
    static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);
    static readonly TimeSpan Grace = TimeSpan.FromSeconds(3); // gaps between songs don't hide the widget

    readonly object _gate = new();
    readonly Timer _timer;
    readonly Dictionary<string, SilenceTimer> _silence = new(StringComparer.OrdinalIgnoreCase);
    List<(string Name, bool ArtistFirst)> _programs = [];
    int _ticking;

    public TitleWatcher() => _timer = new Timer(_ => Tick(), null, Timeout.Infinite, Timeout.Infinite);

    public NowPlaying Current { get; private set; } = NowPlaying.Empty;
    public event Action<NowPlaying>? Changed;

    public void Update(AppSettings s)
    {
        var next = s.Programs
            .Where(p => p.Value.Enabled && p.Value.Mode == ProgramEntry.Title)
            .Select(p => (p.Key, p.Value.Order == ProgramEntry.ArtistTitle))
            .OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();
        lock (_gate)
        {
            _programs = next;
            _timer.Change(next.Count > 0 ? TimeSpan.Zero : Timeout.InfiniteTimeSpan, next.Count > 0 ? Interval : Timeout.InfiniteTimeSpan);
        }
        if (next.Count == 0) Publish(NowPlaying.Empty);
    }

    void Tick()
    {
        if (Interlocked.Exchange(ref _ticking, 1) == 1) return; // a slow read must not pile up ticks
        try
        {
            List<(string Name, bool ArtistFirst)> programs;
            lock (_gate) programs = _programs;
            if (programs.Count == 0) return;
            var audio = AudioActivity.Read();
            var now = DateTimeOffset.UtcNow;
            var found = NowPlaying.Empty;
            foreach (var (name, artistFirst) in programs)
            {
                var (song, pids) = Read(name, artistFirst);
                var sound = audio?.SoundOf(pids); // null: audio unreadable, or played outside Windows sessions (ASIO)
                if (!_silence.TryGetValue(name, out var timer)) _silence[name] = timer = new SilenceTimer(Grace);
                if (!timer.IsPlaying(sound, song is not null, now) || song is null || found.Playing) continue;
                found = new NowPlaying(true, song.Value.Title, song.Value.Artist, "", name, null, null, null, 0, null);
            }
            lock (_gate) if (_programs.Count == 0) return; // turned off while reading
            Publish(found);
        }
        catch { } // one bad read must not stop the timer
        finally { _ticking = 0; }
    }

    /// The first parsable window title among the program's processes, and all their IDs (helpers play the sound too).
    static ((string Title, string Artist)? Song, List<int> Pids) Read(string name, bool artistFirst)
    {
        (string, string)? song = null;
        var pids = new List<int>();
        foreach (var p in Process.GetProcessesByName(name))
        {
            using (p)
            {
                try
                {
                    pids.Add(p.Id);
                    song ??= WindowTitle.Parse(p.MainWindowTitle, name, artistFirst);
                }
                catch { } // exited meanwhile
            }
        }
        return (song, pids);
    }

    void Publish(NowPlaying next)
    {
        lock (_gate)
        {
            if (next == Current) return;
            Current = next;
        }
        Changed?.Invoke(next);
    }

    public void Dispose() => _timer.Dispose();
}
