using Microsoft.Win32;

namespace MusicNowPlayingWidget.Settings;

/// One value under HKCU that makes Windows start this exe at sign-in. The registry is the source of truth.
public sealed class StartupEntry(string keyPath, string valueName, string exePath, string approvedKeyPath)
{
    public static StartupEntry ForCurrentUser(string exePath) =>
        new(@"Software\Microsoft\Windows\CurrentVersion\Run", "MusicNowPlayingWidget", exePath,
            @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run");

    string Command => "\"" + exePath + "\""; // quoted so paths with spaces still run

    // Started some other way (dotnet.exe hosting the dll): registering that would start nothing useful.
    bool IsAppExe => string.Equals(Path.GetFileName(exePath), "MusicNowPlayingWidget.exe", StringComparison.OrdinalIgnoreCase);

    public bool IsEnabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(keyPath);
                if (key?.GetValue(valueName) is not string) return false;
                // Task Manager's "Disable" keeps the Run value and marks it here: an odd first byte means off.
                using var approved = Registry.CurrentUser.OpenSubKey(approvedKeyPath);
                return approved?.GetValue(valueName) is not byte[] { Length: > 0 } flags || (flags[0] & 1) == 0;
            }
            catch { return false; }
        }
    }

    /// Turns the entry on or off and returns what the registry actually holds afterwards.
    public bool Set(bool on)
    {
        try
        {
            if (on && IsAppExe)
            {
                using var key = Registry.CurrentUser.CreateSubKey(keyPath);
                key.SetValue(valueName, Command);
                // Clear a "Disable" from Task Manager, or Windows keeps skipping it.
                using var approved = Registry.CurrentUser.OpenSubKey(approvedKeyPath, writable: true);
                approved?.DeleteValue(valueName, throwOnMissingValue: false);
            }
            else if (!on)
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
        if (!IsAppExe) return false;
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
