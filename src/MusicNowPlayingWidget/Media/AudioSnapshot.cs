namespace MusicNowPlayingWidget.Media;

/// One reading of the Windows audio sessions: which processes have one, and which of those make sound.
public sealed record AudioSnapshot(IReadOnlySet<int> WithSession, IReadOnlySet<int> Audible)
{
    public AudioSnapshot(IEnumerable<int> withSession, IEnumerable<int> audible)
        : this(withSession.ToHashSet(), audible.ToHashSet()) { }

    /// True if any of the processes makes sound, false if they have a session but are silent,
    /// null if none has a session (ASIO or exclusive drivers play outside Windows audio sessions).
    public bool? SoundOf(IReadOnlyCollection<int> pids) =>
        pids.Any(Audible.Contains) ? true : pids.Any(WithSession.Contains) ? false : null;
}
