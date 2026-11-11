namespace MusicNowPlayingWidget.Media;

/// A program counts as playing while it makes sound, and for a short grace after (gaps between songs).
public sealed class SilenceTimer(TimeSpan grace)
{
    DateTimeOffset? _lastHeard;

    /// audible is null when Windows audio can't be read: then a song title alone means playing.
    public bool IsPlaying(bool? audible, bool hasTitle, DateTimeOffset now)
    {
        if (audible is null) return hasTitle;
        if (audible.Value) _lastHeard = now;
        return _lastHeard is { } t && now - t < grace;
    }
}
