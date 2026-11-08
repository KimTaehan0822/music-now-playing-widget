using System.Text.Json;

namespace ObsNowPlaying.Settings;

public sealed class SettingsStore(string path)
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ObsNowPlaying", "settings.json");

    public AppSettings Load()
    {
        try { return (JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), Json) ?? new()).Sanitize(); }
        catch { return new AppSettings(); } // missing or corrupt file: start from defaults
    }

    public void Save(AppSettings s)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(s, Json));
        File.Move(tmp, path, overwrite: true); // write-then-rename so a crash never leaves half a file
    }
}
