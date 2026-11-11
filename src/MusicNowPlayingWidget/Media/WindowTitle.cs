using MusicNowPlayingWidget.Settings;

namespace MusicNowPlayingWidget.Media;

/// Reads "Song - Artist" style window titles of players that don't report to Windows.
public static class WindowTitle
{
    const string Sep = " - ";

    public static (string Title, string Artist)? Parse(string? windowTitle, string programName, bool artistFirst)
    {
        var s = windowTitle?.Trim() ?? "";
        // Many players append their own name: "Song - Artist - foobar2000".
        foreach (var dash in new[] { " - ", " — " })
        {
            var i = s.LastIndexOf(dash, StringComparison.Ordinal);
            if (i >= 0 && ProgramName.SameProgram(s[(i + dash.Length)..], programName))
            {
                s = s[..i].TrimEnd();
                break;
            }
        }
        if (s.Length == 0 || ProgramName.SameProgram(s, programName)) return null; // idle player

        // Song names contain " - " more often than artist names, so split next to the artist.
        var at = artistFirst ? s.IndexOf(Sep, StringComparison.Ordinal) : s.LastIndexOf(Sep, StringComparison.Ordinal);
        if (at < 0) return (s, "");
        var left = s[..at].Trim();
        var right = s[(at + Sep.Length)..].Trim();
        return artistFirst ? (right, left) : (left, right);
    }
}
