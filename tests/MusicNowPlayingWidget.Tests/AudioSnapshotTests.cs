using MusicNowPlayingWidget.Media;
using Xunit;

public class AudioSnapshotTests
{
    static readonly AudioSnapshot Snap = new([10, 11, 20], [11]);

    [Fact]
    public void AnyProcessAudible_IsSound() => Assert.True(Snap.SoundOf([10, 11]));

    [Fact]
    public void SessionButSilent_IsNoSound() => Assert.False(Snap.SoundOf([20]));

    // ASIO or exclusive drivers play outside Windows audio sessions: unknown, so the window title decides.
    [Fact]
    public void NoSessionAtAll_IsUnknown() => Assert.Null(Snap.SoundOf([30, 31]));

    [Fact]
    public void NoProcesses_IsUnknown() => Assert.Null(Snap.SoundOf([]));
}
