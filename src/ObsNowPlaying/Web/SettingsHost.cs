using System.Text.Json;
using System.Text.Json.Nodes;
using ObsNowPlaying.Settings;

namespace ObsNowPlaying.Web;

public sealed record AppEntry(string AppId, bool Enabled);

public sealed class SettingsHost(SettingsStore store, AppSettings initial)
{
    readonly object _gate = new();
    AppSettings _current = initial;

    public event Action<AppSettings>? Changed;

    public AppSettings Get() { lock (_gate) return _current; }

    public void Update(AppSettings next)
    {
        lock (_gate) { _current = next.Sanitize(); store.Save(_current); }
        Changed?.Invoke(_current);
    }

    /// Overlays the JSON fields that are present onto the current settings.
    public void Merge(string json)
    {
        var patch = JsonNode.Parse(json) as JsonObject ?? throw new JsonException("Expected a JSON object");
        AppSettings next;
        lock (_gate)
        {
            var merged = JsonSerializer.SerializeToNode(_current, SettingsStore.Json)!.AsObject();
            foreach (var (key, value) in patch) merged[key] = value?.DeepClone();
            next = merged.Deserialize<AppSettings>(SettingsStore.Json) ?? throw new JsonException("Empty settings");
        }
        Update(next);
    }

    public bool Remember(string appId)
    {
        lock (_gate)
        {
            if (!_current.RememberApp(appId)) return false;
            store.Save(_current);
        }
        Changed?.Invoke(_current);
        return true;
    }

    public IReadOnlyList<AppEntry> Apps()
    {
        lock (_gate) return _current.Apps.Select(a => new AppEntry(a.Key, a.Value)).OrderBy(a => a.AppId).ToList();
    }
}
