using Microsoft.Win32;

namespace MusicNowPlayingWidget.Settings;

/// One value under HKCU that makes Windows start this exe at sign-in. The registry is the source of truth.
public sealed class StartupEntry(string keyPath, string valueName, string exePath)
{
    public static StartupEntry ForCurrentUser(string exePath) =>
        new(@"Software\Microsoft\Windows\CurrentVersion\Run", "MusicNowPlayingWidget", exePath);

    string Command => "\"" + exePath + "\""; // quoted so paths with spaces still run

    public bool IsEnabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(keyPath);
                return key?.GetValue(valueName) is string;
            }
            catch { return false; }
        }
    }

    /// Turns the entry on or off and returns what the registry actually holds afterwards.
    public bool Set(bool on)
    {
        try
        {
            if (on)
            {
                using var key = Registry.CurrentUser.CreateSubKey(keyPath);
                key.SetValue(valueName, Command);
            }
            else
            {
                using var key = Registry.CurrentUser.OpenSubKey(keyPath, writable: true);
                key?.DeleteValue(valueName, throwOnMissingValue: false);
            }
        }
        catch { } // e.g. blocked by policy: the caller shows the real state
        return IsEnabled;
    }

    /// After the folder moved: points an existing entry at this exe. Never creates one.
    public bool Repair()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(keyPath, writable: true);
            if (key?.GetValue(valueName) is not string current) return false;
            if (string.Equals(current, Command, StringComparison.OrdinalIgnoreCase)) return false;
            key.SetValue(valueName, Command);
            return true;
        }
        catch { return false; }
    }
}
