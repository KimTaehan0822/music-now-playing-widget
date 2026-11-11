namespace MusicNowPlayingWidget.Media;

public static class SessionPicker
{
    public static SessionInfo? Pick(IReadOnlyList<SessionInfo> sessions, string? currentAppId, Func<string, bool> isAppEnabled)
    {
        var playing = sessions.Where(s => s.IsPlaying && isAppEnabled(s.AppId)).ToList();
        return playing.FirstOrDefault(s => s.AppId == currentAppId)
            ?? playing.MaxBy(s => s.LastPlayStarted);
    }

    /// A song Windows reports wins; a window title only fills in when nothing there is playing.
    public static NowPlaying Prefer(NowPlaying windows, NowPlaying title) =>
        windows.Playing || !title.Playing ? windows : title;
}
