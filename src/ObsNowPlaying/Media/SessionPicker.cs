namespace ObsNowPlaying.Media;

public static class SessionPicker
{
    public static SessionInfo? Pick(IReadOnlyList<SessionInfo> sessions, string? currentAppId, Func<string, bool> isAppEnabled)
    {
        var playing = sessions.Where(s => s.IsPlaying && isAppEnabled(s.AppId)).ToList();
        return playing.FirstOrDefault(s => s.AppId == currentAppId)
            ?? playing.MaxBy(s => s.LastPlayStarted);
    }
}
