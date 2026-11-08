namespace ObsNowPlaying.Web;

/// Writes a copy of the widget to disk so OBS can open it as a file even before this app starts.
public static class WidgetFiles
{
    public static string DefaultDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ObsNowPlaying", "widget");

    public static string ConfigJs(int port) => $"window.ONP_PORT = {port};\n";

    /// Returns the file:/// URL of widget.html.
    public static string Extract(string dir, int port)
    {
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "widget.html"), ReadText("widget.html"));
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
