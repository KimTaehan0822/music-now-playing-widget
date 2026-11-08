using System.Diagnostics;
using System.Globalization;
using MusicNowPlayingWidget.Localization;

namespace MusicNowPlayingWidget.Tray;

/// The tray icon is the app's only window: open settings, copy the widget URL, restart, quit.
public sealed class TrayApp : ApplicationContext
{
    readonly NotifyIcon _icon;
    readonly SynchronizationContext _ui;
    readonly ToolStripMenuItem _open = new(), _copy = new(), _restart = new(), _quit = new();
    readonly string _baseUrl;
    readonly string _widgetUrl;
    IReadOnlyDictionary<string, string> _strings = new Dictionary<string, string>();

    public TrayApp(int port, string widgetUrl, string? language)
    {
        _baseUrl = $"http://localhost:{port}";
        _widgetUrl = widgetUrl;
        _open.Click += (_, _) => OpenSettings();
        _copy.Click += (_, _) => CopyWidgetUrl();
        _restart.Click += (_, _) => { RestartRequested = true; _icon!.Visible = false; ExitThread(); };
        _quit.Click += (_, _) => { _icon!.Visible = false; ExitThread(); };
        var menu = new ContextMenuStrip(); // creating a control installs the WinForms sync context
        _ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        menu.Items.AddRange([_open, _copy, new ToolStripSeparator(), _restart, _quit]);
        _icon = new NotifyIcon
        {
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application,
            ContextMenuStrip = menu,
            Text = "Music Now Playing Widget",
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
        _restart.Text = _strings["tray.restart"];
        _quit.Text = _strings["tray.quit"];
    }, null);

    /// Set when the user picked "Restart"; Program starts a fresh copy after this one shuts down.
    public bool RestartRequested { get; private set; }

    public void OpenSettings() => OpenUrl($"{_baseUrl}/settings");

    public static void OpenUrl(string url) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

    void CopyWidgetUrl()
    {
        Clipboard.SetText(_widgetUrl);
        _icon.ShowBalloonTip(4000, "Music Now Playing Widget", _strings["tray.urlCopied"], ToolTipIcon.Info);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _icon.Dispose();
        base.Dispose(disposing);
    }
}
