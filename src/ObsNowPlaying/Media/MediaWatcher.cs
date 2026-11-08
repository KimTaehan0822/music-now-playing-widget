using System.Security.Cryptography;
using Windows.Media.Control;
using Windows.Storage.Streams;
using GsmtcManager = Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager;
using GsmtcProps = Windows.Media.Control.GlobalSystemMediaTransportControlsSessionMediaProperties;
using GsmtcSession = Windows.Media.Control.GlobalSystemMediaTransportControlsSession;

namespace ObsNowPlaying.Media;

/// Reads the Windows media sessions (GSMTC) and reports the one the widget should show.
public sealed class MediaWatcher(Func<string, bool> isAppEnabled)
{
    // Some players publish the new title a moment before its cover; keep re-reading the cover this long.
    static readonly TimeSpan ArtSettleTime = TimeSpan.FromSeconds(5);

    readonly SemaphoreSlim _refreshGate = new(1, 1);
    readonly List<GsmtcSession> _subscribed = [];
    // Keyed by app + title + artist, so two tabs of the same browser don't overwrite each other.
    Dictionary<string, DateTimeOffset> _playStarted = [];
    GsmtcManager? _manager;
    int _dirty;
    string? _artKey;
    DateTimeOffset _artKeyAt;

    public event Action<NowPlaying>? Changed;
    public event Action<string>? AppSeen;

    public NowPlaying Current { get; private set; } = NowPlaying.Empty;
    public AlbumArt? Art { get; private set; }

    public async Task StartAsync()
    {
        _manager = await GsmtcManager.RequestAsync();
        _manager.SessionsChanged += (_, _) => { Resubscribe(); Refresh(); };
        _manager.CurrentSessionChanged += (_, _) => Refresh();
        Resubscribe();
        Refresh();
    }

    void Resubscribe()
    {
        lock (_subscribed)
        {
            foreach (var s in _subscribed)
            {
                s.MediaPropertiesChanged -= OnSessionChanged;
                s.PlaybackInfoChanged -= OnSessionChanged;
                s.TimelinePropertiesChanged -= OnSessionChanged;
            }
            _subscribed.Clear();
            foreach (var s in _manager!.GetSessions())
            {
                s.MediaPropertiesChanged += OnSessionChanged;
                s.PlaybackInfoChanged += OnSessionChanged;
                s.TimelinePropertiesChanged += OnSessionChanged;
                _subscribed.Add(s);
            }
        }
    }

    void OnSessionChanged(GsmtcSession sender, object args) => Refresh();

    /// Re-reads every session and picks again; call it when the app filter changes too.
    /// Bursts of events collapse: a call during a refresh only asks for one more pass.
    public void Refresh()
    {
        if (Interlocked.Exchange(ref _dirty, 1) == 0) _ = Task.Run(RefreshLoopAsync);
    }

    async Task RefreshLoopAsync()
    {
        if (_manager is null) { _dirty = 0; return; } // not started; StartAsync refreshes anyway
        await _refreshGate.WaitAsync();
        try
        {
            while (Interlocked.Exchange(ref _dirty, 0) == 1)
            {
                try { await RefreshOnceAsync(_manager); }
                catch { } // one bad read must not stop future refreshes
            }
        }
        finally { _refreshGate.Release(); }
    }

    async Task RefreshOnceAsync(GsmtcManager manager)
    {
        var started = new Dictionary<string, DateTimeOffset>();
        var infos = new List<(SessionInfo Info, GsmtcProps? Props)>();
        foreach (var s in manager.GetSessions())
        {
            try
            {
                var props = await s.TryGetMediaPropertiesAsync();
                var playing = s.GetPlaybackInfo()?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
                var app = s.SourceAppUserModelId;
                var playKey = string.Join('\n', app, props?.Title, props?.Artist);
                if (playing) started[playKey] = _playStarted.GetValueOrDefault(playKey, DateTimeOffset.UtcNow);
                var tl = s.GetTimelineProperties();
                var duration = tl.EndTime - tl.StartTime;
                var info = new SessionInfo(app, props?.Title ?? "", props?.Artist ?? "", props?.AlbumTitle ?? "", playing,
                    started.GetValueOrDefault(playKey), duration > TimeSpan.Zero ? tl.Position : null,
                    duration > TimeSpan.Zero ? duration : null, tl.LastUpdatedTime);
                infos.Add((info, props));
                AppSeen?.Invoke(app);
            }
            catch { } // a session that vanished mid-read is simply skipped
        }
        _playStarted = started;

        var currentApp = manager.GetCurrentSession()?.SourceAppUserModelId;
        var picked = SessionPicker.Pick(infos.Select(i => i.Info).ToList(), currentApp, isAppEnabled);
        NowPlaying next;
        if (picked is null || string.IsNullOrEmpty(picked.Title))
        {
            next = NowPlaying.Empty;
        }
        else
        {
            await LoadArtAsync(picked, infos.First(i => i.Info == picked).Props);
            next = new NowPlaying(true, picked.Title, picked.Artist, picked.Album, picked.AppId, Art?.Version,
                (long?)picked.Position?.TotalMilliseconds, (long?)picked.Duration?.TotalMilliseconds,
                picked.PositionAt.ToUnixTimeMilliseconds(), Art?.Palette);
        }

        if (next != Current)
        {
            Current = next;
            Changed?.Invoke(next);
        }
    }

    async Task LoadArtAsync(SessionInfo s, GsmtcProps? props)
    {
        var key = string.Join('\n', s.AppId, s.Title, s.Artist, s.Album);
        var now = DateTimeOffset.UtcNow;
        if (key != _artKey)
        {
            _artKey = key;
            _artKeyAt = now;
            Art = null;
        }
        else if (Art is not null && now - _artKeyAt > ArtSettleTime)
        {
            return; // settled: no need to re-read the same cover
        }
        if (props?.Thumbnail is null) return;
        try
        {
            using IRandomAccessStreamWithContentType stream = await props.Thumbnail.OpenReadAsync();
            using var ms = new MemoryStream();
            await stream.AsStreamForRead().CopyToAsync(ms);
            if (ms.Length == 0) return;
            var bytes = ms.ToArray();
            var version = Convert.ToHexString(SHA1.HashData(bytes))[..8];
            if (version != Art?.Version)
                Art = new AlbumArt(bytes, string.IsNullOrEmpty(stream.ContentType) ? "image/jpeg" : stream.ContentType, version,
                    ArtPalette.From(bytes)); // once per cover
        }
        catch { } // keep what we have (text only if nothing); the next change tries again
    }
}
