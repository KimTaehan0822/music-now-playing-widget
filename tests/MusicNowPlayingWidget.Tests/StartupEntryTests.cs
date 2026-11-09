using Microsoft.Win32;
using MusicNowPlayingWidget.Settings;
using Xunit;

public class StartupEntryTests : IDisposable
{
    const string Root = @"Software\MusicNowPlayingWidgetTests";
    readonly string _key = Root + @"\" + Guid.NewGuid().ToString("N");

    public void Dispose() => Registry.CurrentUser.DeleteSubKeyTree(_key, throwOnMissingSubKey: false);

    StartupEntry Entry(string exe) => new(_key, "MusicNowPlayingWidget", exe);

    object? Raw()
    {
        using var k = Registry.CurrentUser.OpenSubKey(_key);
        return k?.GetValue("MusicNowPlayingWidget");
    }

    [Fact]
    public void StartsDisabled() => Assert.False(Entry(@"C:\w\w.exe").IsEnabled);

    [Fact]
    public void Enable_QuotesPath()
    {
        Assert.True(Entry(@"C:\My Tools\w.exe").Set(true));
        Assert.Equal("\"C:\\My Tools\\w.exe\"", Raw());
    }

    [Fact]
    public void Disable_RemovesValue()
    {
        var e = Entry(@"C:\w\w.exe");
        Assert.False(e.Set(false)); // nothing there yet: no throw
        e.Set(true);
        Assert.False(e.Set(false));
        Assert.Null(Raw());
    }

    [Fact]
    public void Repair_RewritesMovedPath()
    {
        Entry(@"C:\old\w.exe").Set(true);
        Assert.True(Entry(@"D:\new place\w.exe").Repair());
        Assert.Equal("\"D:\\new place\\w.exe\"", Raw());
    }

    [Fact]
    public void Repair_DoesNothingWhenOff()
    {
        Assert.False(Entry(@"C:\w\w.exe").Repair());
        Assert.Null(Raw());
    }

    [Fact]
    public void Repair_SamePath_NoWrite()
    {
        Entry(@"C:\w\w.exe").Set(true);
        Assert.False(Entry(@"c:\W\w.exe").Repair());
    }
}
