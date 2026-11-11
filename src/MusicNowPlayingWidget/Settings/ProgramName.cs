namespace MusicNowPlayingWidget.Settings;

/// Compares a process name with the app ID Windows reports, e.g. "Melon Player" with "com.kakaoent.melon.player".
public static class ProgramName
{
    public static string Normalize(string s) =>
        string.Concat(s.Where(char.IsLetterOrDigit)).ToLowerInvariant();

    public static bool Matches(string programName, string appId)
    {
        var name = Normalize(programName);
        return name.Length > 0 && Normalize(appId).Contains(name);
    }
}
