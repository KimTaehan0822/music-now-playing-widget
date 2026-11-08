using System.Text.Json;
using System.Text.Json.Nodes;

namespace MusicNowPlayingWidget.Settings;

public sealed class SettingsStore(string path)
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MusicNowPlayingWidget", "settings.json");

    /// Where versions before the rename (called "ObsNowPlaying") kept their settings.
    public static string LegacyPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ObsNowPlaying", "settings.json");

    /// Copies settings from an older location once, never over existing ones. The old file stays.
    public static bool Migrate(string oldPath, string newPath)
    {
        if (File.Exists(newPath) || !File.Exists(oldPath)) return false;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(newPath)!);
            File.Copy(oldPath, newPath);
            return true;
        }
        catch { return false; } // unreadable old file: start fresh rather than fail to launch
    }

    public AppSettings Load()
    {
        var settings = new AppSettings();
        JsonObject? json;
        try { json = JsonNode.Parse(File.ReadAllText(path)) as JsonObject; }
        catch { return settings; } // missing or unreadable file: start from defaults
        if (json is null) return settings;
        // Field by field, so one mistyped value (e.g. edited in Notepad) only resets that field.
        foreach (var prop in typeof(AppSettings).GetProperties())
        {
            if (!json.TryGetPropertyValue(Json.PropertyNamingPolicy!.ConvertName(prop.Name), out var node) || node is null) continue;
            try { prop.SetValue(settings, node.Deserialize(prop.PropertyType, Json)); }
            catch { }
        }
        return settings.Sanitize();
    }

    public void Save(AppSettings s)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(s, Json));
        File.Move(tmp, path, overwrite: true); // write-then-rename so a crash never leaves half a file
    }
}
