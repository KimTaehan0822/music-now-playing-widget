using System.Text.RegularExpressions;

namespace ObsNowPlaying.Settings;

public sealed class AppSettings
{
    // Substrings of the AppUserModelIds browsers report to Windows; 308046B0AF4A39CB is Firefox's.
    static readonly string[] BrowserMarks =
        ["chrome", "msedge", "microsoftedge", "whale", "firefox", "308046B0AF4A39CB", "opera", "brave", "vivaldi"];

    public int Port { get; set; } = 8976;
    public string? Language { get; set; }
    public string DefaultStyle { get; set; } = "A";
    public string TextColor { get; set; } = "#FFFFFF";
    public string BackgroundColor { get; set; } = "#14161C";
    public string AccentColor { get; set; } = "#1DB954";
    public bool ShowArt { get; set; } = true;
    public bool ShowProgress { get; set; } = true;
    public bool ShowTime { get; set; } = true;
    public bool AutoColors { get; set; } // follow each album cover's colors; off until the user opts in
    public Dictionary<string, bool> Apps { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public static bool IsKnownBrowser(string appId) =>
        BrowserMarks.Any(m => appId.Contains(m, StringComparison.OrdinalIgnoreCase));

    public bool IsAppEnabled(string appId) =>
        Apps.TryGetValue(appId, out var on) ? on : !IsKnownBrowser(appId);

    public bool RememberApp(string appId) => Apps.TryAdd(appId, !IsKnownBrowser(appId));

    public AppSettings Sanitize()
    {
        var d = new AppSettings();
        if (DefaultStyle is not ("A" or "B" or "C")) DefaultStyle = d.DefaultStyle;
        TextColor = Color(TextColor, d.TextColor);
        BackgroundColor = Color(BackgroundColor, d.BackgroundColor);
        AccentColor = Color(AccentColor, d.AccentColor);
        if (Language is not (null or "ko" or "en" or "ja")) Language = null;
        if (Port is < 1024 or > 65535) Port = d.Port;
        Apps = new Dictionary<string, bool>(Apps ?? [], StringComparer.OrdinalIgnoreCase);
        return this;
    }

    static string Color(string? value, string fallback) =>
        value is not null && Regex.IsMatch(value, "^#[0-9A-Fa-f]{6}$") ? value : fallback;
}
