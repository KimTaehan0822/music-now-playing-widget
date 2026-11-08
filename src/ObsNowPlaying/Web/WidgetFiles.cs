namespace ObsNowPlaying.Web;

/// Writes a copy of the widget to disk so OBS can open it as a file even before this app starts.
public static class WidgetFiles
{
    public static string DefaultDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ObsNowPlaying", "widget");

    /// Changes whenever the widget's files change, so OBS never keeps running an older copy.
    public static string Build { get; } = Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(
        new[] { "widget.html", "widget.js", "widget.css" }.SelectMany(ReadBytes).ToArray()))[..8];

    public static string ConfigJs(int port) => $"window.ONP_PORT = {port};\nwindow.ONP_BUILD = \"{Build}\";\n";

    /// Returns the file:/// URL of widget.html.
    public static string Extract(string dir, int port)
    {
        Directory.CreateDirectory(dir);
        // OBS can keep a cached widget.js even after "Refresh cache"; a new address per build forces a fresh read.
        var v = $"?v={Build}-{port}";
        File.WriteAllText(Path.Combine(dir, "widget.html"), ReadText("widget.html")
            .Replace("\"widget.css\"", $"\"widget.css{v}\"")
            .Replace("\"config.js\"", $"\"config.js{v}\"")
            .Replace("\"widget.js\"", $"\"widget.js{v}\""));
        File.WriteAllText(Path.Combine(dir, "widget.js"), ReadText("widget.js"));
        File.WriteAllText(Path.Combine(dir, "config.js"), ConfigJs(port));
        // A file:// page may not load a font from a sibling file, so the font goes inside the CSS.
        var font = Convert.ToBase64String(ReadBytes("fonts/PretendardVariable.woff2"));
        File.WriteAllText(Path.Combine(dir, "widget.css"), ReadText("widget.css")
            .Replace("url(\"fonts/PretendardVariable.woff2\")", $"url(\"data:font/woff2;base64,{font}\")"));
        return new Uri(Path.Combine(dir, "widget.html")).AbsoluteUri;
    }

    static byte[] ReadBytes(string name)
    {
        using var stream = typeof(WidgetFiles).Assembly.GetManifestResourceStream("wwwroot/" + name)
            ?? throw new FileNotFoundException(name);
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    static string ReadText(string name) => System.Text.Encoding.UTF8.GetString(ReadBytes(name));
}
