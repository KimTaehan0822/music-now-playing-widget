using Microsoft.Win32;
using MusicNowPlayingWidget.Settings;
using Xunit;

public class StartupEntryTests : IDisposable
{
    const string Root = @"Software\MusicNowPlayingWidgetTests\";
    readonly string _key = Root + Guid.NewGuid().ToString("N");
    string Approved => _key + @"\approved";

    public void Dispose()
    {
        Registry.CurrentUser.DeleteSubKeyTree(_key, throwOnMissingSubKey: false);
        using var root = Registry.CurrentUser.OpenSubKey(Root, writable: true);
        if (root is { SubKeyCount: 0, ValueCount: 0 })
        {
            root.Close();
            Registry.CurrentUser.DeleteSubKey(Root, throwOnMissingSubKey: false);
        }
    }

    StartupEntry Entry(string exe) => new(_key, "MusicNowPlayingWidget", exe, Approved);

    object? Raw(string? key = null)
    {
        using var k = Registry.CurrentUser.OpenSubKey(key ?? _key);
        return k?.GetValue("MusicNowPlayingWidget");
    }

    // What Task Manager writes when the user turns an app off under "Startup apps".
    void DisableInTaskManager()
    {
        using var k = Registry.CurrentUser.CreateSubKey(Approved);
        k.SetValue("MusicNowPlayingWidget", new byte[] { 3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, RegistryValueKind.Binary);
    }

    const string Exe = @"C:\w\MusicNowPlayingWidget.exe";

    [Fact]
    public void StartsDisabled() => Assert.False(Entry(Exe).IsEnabled);

    [Fact]
    public void Enable_QuotesPath()
    {
        Assert.True(Entry(@"C:\My Tools\MusicNowPlayingWidget.exe").Set(true));
        Assert.Equal("\"C:\\My Tools\\MusicNowPlayingWidget.exe\"", Raw());
    }

    [Fact]
    public void Disable_RemovesValue()
    {
        var e = Entry(Exe);
        Assert.False(e.Set(false)); // nothing there yet: no throw
        e.Set(true);
        Assert.False(e.Set(false));
        Assert.Null(Raw());
    }

    [Fact]
    public void DisabledInTaskManager_ReadsOff()
    {
        var e = Entry(Exe);
        e.Set(true);
        DisableInTaskManager();
        Assert.False(e.IsEnabled);
    }

    [Fact]
    public void Enable_ClearsTaskManagerDisable()
    {
        var e = Entry(Exe);
        e.Set(true);
        DisableInTaskManager();
        Assert.True(e.Set(true));
        Assert.Null(Raw(Approved));
    }

    [Fact]
    public void OtherHost_IsNotRegistered()
    {
        // e.g. started as `dotnet MusicNowPlayingWidget.dll`: Windows would start dotnet.exe alone.
        Assert.False(Entry(@"C:\Program Files\dotnet\dotnet.exe").Set(true));
        Assert.Null(Raw());
    }

    [Fact]
    public void Repair_RewritesMovedPath()
    {
        Entry(@"C:\old\MusicNowPlayingWidget.exe").Set(true);
        Assert.True(Entry(@"D:\new place\MusicNowPlayingWidget.exe").Repair());
        Assert.Equal("\"D:\\new place\\MusicNowPlayingWidget.exe\"", Raw());
    }

    [Fact]
    public void Repair_IgnoresOtherHost()
    {
        Entry(Exe).Set(true);
        Assert.False(Entry(@"C:\Program Files\dotnet\dotnet.exe").Repair());
        Assert.Equal("\"" + Exe + "\"", Raw());
    }

    [Fact]
    public void Repair_DoesNothingWhenOff()
    {
        Assert.False(Entry(Exe).Repair());
        Assert.Null(Raw());
    }

    [Fact]
    public void Repair_SamePath_NoWrite()
    {
        Entry(Exe).Set(true);
        Assert.False(Entry(@"c:\W\musicnowplayingwidget.exe").Repair());
    }
}
