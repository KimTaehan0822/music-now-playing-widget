using System.Diagnostics;
using System.Globalization;
using ObsNowPlaying.Localization;

namespace ObsNowPlaying.Tray;

/// The tray icon is the app's only window: open settings, copy the widget URL, quit.
public sealed class TrayApp : ApplicationContext
{
    readonly NotifyIcon _icon;
    readonly SynchronizationContext _ui;
    readonly ToolStripMenuItem _open = new(), _copy = new(), _quit = new();
    readonly string _baseUrl;
    IReadOnlyDictionary<string, string> _strings = new Dictionary<string, string>();

    public TrayApp(int port, string? language)
    {
        _baseUrl = $"http://localhost:{port}";
        _open.Click += (_, _) => OpenSettings();
        _copy.Click += (_, _) => CopyWidgetUrl();
        _quit.Click += (_, _) => { _icon!.Visible = false; ExitThread(); };
        var menu = new ContextMenuStrip(); // creating a control installs the WinForms sync context
        _ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        menu.Items.AddRange([_open, _copy, new ToolStripSeparator(), _quit]);
        _icon = new NotifyIcon
        {
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application,
            ContextMenuStrip = menu,
            Text = "OBS Now Playing",
            Visible = true,
        };
        _icon.DoubleClick += (_, _) => OpenSettings();
        SetLanguage(language);
    }

    /// Safe to call from any thread.
    public void SetLanguage(string? setting) => _ui.Post(_ =>
    {
        _strings = Strings.Load(Strings.ResolveLanguage(setting, CultureInfo.CurrentUICulture));
        _open.Text = _strings["tray.openSettings"];
        _copy.Text = _strings["tray.copyWidgetUrl"];
        _quit.Text = _strings["tray.quit"];
    }, null);

    public void OpenSettings() => OpenUrl($"{_baseUrl}/settings");

    public static void OpenUrl(string url) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

    void CopyWidgetUrl()
    {
        Clipboard.SetText($"{_baseUrl}/widget");
        _icon.ShowBalloonTip(4000, "OBS Now Playing", _strings["tray.urlCopied"], ToolTipIcon.Info);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _icon.Dispose();
        base.Dispose(disposing);
    }
}
