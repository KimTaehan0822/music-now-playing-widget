using System.Globalization;
using System.Net;
using ObsNowPlaying.Localization;
using ObsNowPlaying.Media;
using ObsNowPlaying.Settings;
using ObsNowPlaying.Tray;
using ObsNowPlaying.Web;

namespace ObsNowPlaying;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        var firstRun = !File.Exists(SettingsStore.DefaultPath);
        var store = new SettingsStore(SettingsStore.DefaultPath);
        var settings = new SettingsHost(store, store.Load());
        var port = settings.Get().Port;

        using var single = new Mutex(true, @"Local\ObsNowPlaying", out var isFirstInstance);
        if (!isFirstInstance)
        {
            TrayApp.OpenUrl($"http://localhost:{port}/settings"); // already running: just show its settings
            return;
        }

        var watcher = new MediaWatcher(app => settings.Get().IsAppEnabled(app));
        using var server = new WebServer(port, () => watcher.Current, () => (watcher.ArtBytes, watcher.ArtContentType), settings);
        try
        {
            server.Start();
        }
        catch (HttpListenerException)
        {
            var lang = Strings.ResolveLanguage(settings.Get().Language, CultureInfo.CurrentUICulture);
            MessageBox.Show(Strings.Load(lang)["error.portInUse"].Replace("{port}", port.ToString()),
                "OBS Now Playing", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        using var tray = new TrayApp(port, settings.Get().Language);
        watcher.Changed += np => server.Broadcast("nowplaying", np);
        watcher.AppSeen += app => settings.Remember(app);
        settings.Changed += s =>
        {
            server.Broadcast("settings", s);
            tray.SetLanguage(s.Language);
            watcher.Refresh();
        };

        watcher.StartAsync().GetAwaiter().GetResult();
        if (firstRun) tray.OpenSettings(); // first launch: show people where things are
        Application.Run(tray);
    }
}
