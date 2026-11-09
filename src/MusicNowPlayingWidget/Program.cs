using System.Globalization;
using System.Net;
using MusicNowPlayingWidget.Localization;
using MusicNowPlayingWidget.Media;
using MusicNowPlayingWidget.Settings;
using MusicNowPlayingWidget.Tray;
using MusicNowPlayingWidget.Web;

namespace MusicNowPlayingWidget;

static class Program
{
    const string RestartArg = "--restart";

    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        SettingsStore.Migrate(SettingsStore.LegacyPath, SettingsStore.DefaultPath);
        var firstRun = !File.Exists(SettingsStore.DefaultPath);
        var store = new SettingsStore(SettingsStore.DefaultPath);
        var settings = new SettingsHost(store, store.Load());
        var port = settings.Get().Port;

        using var single = new Mutex(false, @"Local\MusicNowPlayingWidget");
        bool isFirstInstance;
        // A restart waits for the old copy to let go of the mutex (and the port) instead of giving up.
        try { isFirstInstance = single.WaitOne(args.Contains(RestartArg) ? TimeSpan.FromSeconds(15) : TimeSpan.Zero); }
        catch (AbandonedMutexException) { isFirstInstance = true; } // the old copy exited without releasing: it's ours
        if (!isFirstInstance)
        {
            TrayApp.OpenUrl($"http://localhost:{port}/settings"); // already running: just show its settings
            return;
        }

        // OBS opens this file, which loads even while the app is off and connects once it starts.
        string widgetUrl;
        try { widgetUrl = WidgetFiles.Extract(WidgetFiles.DefaultDir, port); }
        catch { widgetUrl = $"http://localhost:{port}/widget"; } // can't write the file: the web address still works

        var startup = StartupEntry.ForCurrentUser(Environment.ProcessPath!);
        startup.Repair(); // the folder may have moved since the user turned this on

        var watcher = new MediaWatcher(app => settings.Get().IsAppEnabled(app));
        using var server = new WebServer(port, () => watcher.Current, () => watcher.Art, settings, widgetUrl, startup);
        try
        {
            server.Start();
        }
        catch (HttpListenerException)
        {
            var lang = Strings.ResolveLanguage(settings.Get().Language, CultureInfo.CurrentUICulture);
            MessageBox.Show(Strings.Load(lang)["error.portInUse"].Replace("{port}", port.ToString()),
                "Music Now Playing Widget", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        TrayApp? tray = null;
        watcher.Changed += np => server.Broadcast("nowplaying", np);
        watcher.AppSeen += app => settings.Remember(app);
        settings.Changed += s =>
        {
            server.Broadcast("settings", s);
            tray?.SetLanguage(s.Language);
            watcher.Refresh();
        };

        // Before any WinForms control exists: blocking here with a WinForms sync context could deadlock.
        watcher.StartAsync().GetAwaiter().GetResult();
        using var trayApp = tray = new TrayApp(port, widgetUrl, settings.Get().Language);
        if (firstRun) tray.OpenSettings(); // first launch: show people where things are
        Application.Run(trayApp);
        if (trayApp.RestartRequested)
            System.Diagnostics.Process.Start(Environment.ProcessPath!, RestartArg); // waits for this copy to exit
    }
}
