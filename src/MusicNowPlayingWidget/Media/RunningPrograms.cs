using System.Diagnostics;

namespace MusicNowPlayingWidget.Media;

/// Names of the programs that have a window open, for the settings page to pick from.
public static class RunningPrograms
{
    public static IReadOnlyList<string> List()
    {
        var own = Process.GetCurrentProcess().ProcessName;
        var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in Process.GetProcesses())
        {
            using (p)
            {
                try
                {
                    if (p.MainWindowHandle != IntPtr.Zero && !p.ProcessName.Equals(own, StringComparison.OrdinalIgnoreCase))
                        names.Add(p.ProcessName);
                }
                catch { } // exited meanwhile, or not ours to read
            }
        }
        return [.. names];
    }
}
