using System.Security.Cryptography;
using Windows.Media.Control;
using Windows.Storage.Streams;
using GsmtcManager = Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager;
using GsmtcSession = Windows.Media.Control.GlobalSystemMediaTransportControlsSession;

namespace ObsNowPlaying.Media;

/// Reads the Windows media sessions (GSMTC) and reports the one the widget should show.
public sealed class MediaWatcher(Func<string, bool> isAppEnabled)
{
    readonly SemaphoreSlim _refreshGate = new(1, 1);
    readonly Dictionary<string, DateTimeOffset> _playStarted = [];
    readonly List<GsmtcSession> _subscribed = [];
    GsmtcManager? _manager;
    string? _artKey;

    public event Action<NowPlaying>? Changed;
    public event Action<string>? AppSeen;

    public NowPlaying Current { get; private set; } = NowPlaying.Empty;
    public byte[]? ArtBytes { get; private set; }
    public string? ArtContentType { get; private set; }

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
    public void Refresh() => _ = RefreshAsync();

    async Task RefreshAsync()
    {
        if (_manager is null) return;
        await _refreshGate.WaitAsync();
        try
        {
            var infos = new List<(SessionInfo Info, GsmtcSession Session, GlobalSystemMediaTransportControlsSessionMediaProperties Props)>();
            foreach (var s in _manager.GetSessions())
            {
                try
                {
                    var props = await s.TryGetMediaPropertiesAsync();
                    var playing = s.GetPlaybackInfo()?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
                    var app = s.SourceAppUserModelId;
                    if (playing) _playStarted.TryAdd(app, DateTimeOffset.UtcNow);
                    else _playStarted.Remove(app);
                    var tl = s.GetTimelineProperties();
                    var duration = tl.EndTime - tl.StartTime;
                    var info = new SessionInfo(app, props?.Title ?? "", props?.Artist ?? "", props?.AlbumTitle ?? "", playing,
                        _playStarted.GetValueOrDefault(app), duration > TimeSpan.Zero ? tl.Position : null,
                        duration > TimeSpan.Zero ? duration : null, tl.LastUpdatedTime);
                    infos.Add((info, s, props!));
                    AppSeen?.Invoke(app);
                }
                catch { } // a session that vanished mid-read is simply skipped
            }

            var currentApp = _manager.GetCurrentSession()?.SourceAppUserModelId;
            var picked = SessionPicker.Pick(infos.Select(i => i.Info).ToList(), currentApp, isAppEnabled);
            NowPlaying next;
            if (picked is null || string.IsNullOrEmpty(picked.Title))
            {
                next = NowPlaying.Empty;
            }
            else
            {
                var entry = infos.First(i => i.Info == picked);
                await LoadArtAsync(picked, entry.Props);
                next = new NowPlaying(true, picked.Title, picked.Artist, picked.Album, picked.AppId,
                    ArtBytes is null ? null : Convert.ToHexString(SHA1.HashData(ArtBytes))[..8],
                    (long?)picked.Position?.TotalMilliseconds, (long?)picked.Duration?.TotalMilliseconds,
                    picked.PositionAt.ToUnixTimeMilliseconds());
            }

            if (next != Current)
            {
                Current = next;
                Changed?.Invoke(next);
            }
        }
        finally { _refreshGate.Release(); }
    }

    async Task LoadArtAsync(SessionInfo s, GlobalSystemMediaTransportControlsSessionMediaProperties? props)
    {
        var key = $"{s.AppId}\n{s.Title}\n{s.Artist}\n{s.Album}";
        if (key == _artKey && ArtBytes is not null) return;
        _artKey = key;
        ArtBytes = null;
        ArtContentType = null;
        if (props?.Thumbnail is null) return;
        try
        {
            using IRandomAccessStreamWithContentType stream = await props.Thumbnail.OpenReadAsync();
            using var ms = new MemoryStream();
            await stream.AsStreamForRead().CopyToAsync(ms);
            ArtBytes = ms.Length > 0 ? ms.ToArray() : null;
            ArtContentType = string.IsNullOrEmpty(stream.ContentType) ? "image/jpeg" : stream.ContentType;
        }
        catch
        {
            _artKey = null; // try again on the next change; show text only meanwhile
        }
    }
}
