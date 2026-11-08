using System.Globalization;
using System.Text.Json;

namespace ObsNowPlaying.Localization;

public static class Strings
{
    public static readonly string[] Languages = ["ko", "en", "ja"];

    public static string ResolveLanguage(string? setting, CultureInfo uiCulture) =>
        setting is not null && Languages.Contains(setting) ? setting
        : uiCulture.TwoLetterISOLanguageName is "ko" or "ja" ? uiCulture.TwoLetterISOLanguageName
        : "en";

    public static IReadOnlyDictionary<string, string> Load(string lang)
    {
        using var stream = typeof(Strings).Assembly.GetManifestResourceStream($"wwwroot/i18n/{lang}.json")
            ?? throw new ArgumentException($"Unknown language: {lang}");
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)!;
    }
}
