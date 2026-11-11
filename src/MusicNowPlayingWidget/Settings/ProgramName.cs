using System.Text.RegularExpressions;

namespace MusicNowPlayingWidget.Settings;

/// Compares a process name with the app ID Windows reports, e.g. "Melon Player" with "com.kakaoent.melon.player".
public static class ProgramName
{
    public static string Normalize(string s) =>
        string.Concat(s.Where(char.IsLetterOrDigit)).ToLowerInvariant();

    /// True when the name spells out one or more whole words of the app ID in a row ("foo" doesn't match "foobar.exe").
    /// A site installed from a browser ("Chrome._crx_...") never matches: its process is the browser.
    public static bool Matches(string programName, string appId)
    {
        var name = Normalize(programName);
        if (name.Length == 0 || appId.Contains("_crx_", StringComparison.OrdinalIgnoreCase)) return false;
        var words = Regex.Split(appId.ToLowerInvariant(), @"[^\p{L}\p{N}]+").Where(w => w.Length > 0).ToArray();
        for (var i = 0; i < words.Length; i++)
        {
            var run = "";
            for (var j = i; j < words.Length && run.Length < name.Length; j++)
            {
                run += words[j];
                if (run == name) return true;
            }
        }
        return false;
    }

    /// The same program written either way round, e.g. "Melon" in a window title for the process "Melon Player".
    public static bool SameProgram(string a, string b) => Matches(a, b) || Matches(b, a);
}
