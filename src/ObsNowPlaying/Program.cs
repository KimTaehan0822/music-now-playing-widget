using ObsNowPlaying.Media;
using ObsNowPlaying.Settings;
using ObsNowPlaying.Web;

namespace ObsNowPlaying;

static class Program
{
    [STAThread]
    static void Main()
    {
        var store = new SettingsStore(SettingsStore.DefaultPath);
        var settings = new SettingsHost(store, store.Load());
        var watcher = new MediaWatcher(app => settings.Get().IsAppEnabled(app));
        using var server = new WebServer(settings.Get().Port, () => watcher.Current, () => (watcher.ArtBytes, watcher.ArtContentType), settings);

        watcher.Changed += np => server.Broadcast("nowplaying", np);
        watcher.AppSeen += app => settings.Remember(app);
        settings.Changed += s => { server.Broadcast("settings", s); watcher.Refresh(); };

        server.Start();
        watcher.StartAsync().GetAwaiter().GetResult();
        Application.Run(new ApplicationContext());
    }
}
