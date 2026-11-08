namespace ObsNowPlaying.Media;

public sealed record SessionInfo(
    string AppId, string Title, string Artist, string Album, bool IsPlaying,
    DateTimeOffset LastPlayStarted, TimeSpan? Position, TimeSpan? Duration, DateTimeOffset PositionAt);

public sealed record AlbumArt(byte[] Bytes, string ContentType, string Version);

public sealed record NowPlaying(
    bool Playing, string Title, string Artist, string Album, string AppId,
    string? ArtVersion, long? PositionMs, long? DurationMs, long PositionAtMs)
{
    public static readonly NowPlaying Empty = new(false, "", "", "", "", null, null, null, 0);
}
