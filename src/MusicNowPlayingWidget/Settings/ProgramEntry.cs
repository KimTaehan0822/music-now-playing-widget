namespace MusicNowPlayingWidget.Settings;

/// A program the user picked from the running ones, keyed by its process name in AppSettings.Programs.
public sealed class ProgramEntry
{
    public const string Windows = "windows";       // wait for it in the Windows media sessions
    public const string Title = "title";           // read the song from its window title
    public const string TitleArtist = "title-artist";
    public const string ArtistTitle = "artist-title";

    public bool Enabled { get; set; } = true;
    public string Mode { get; set; } = Windows;
    public string Order { get; set; } = TitleArtist;
}
