using System.Collections.Concurrent;
using System.Text.Json;

namespace ImageDeduper.Core.Localization;

public sealed class Translator
{
    private readonly IReadOnlyDictionary<string, string> _baseTranslations;
    private readonly IReadOnlyDictionary<string, string> _targetTranslations;

    private static readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, string>> Cache = new();

    public Translator(string languageCode)
    {
        var normalized = (languageCode ?? "en").Trim().ToLowerInvariant();
        _baseTranslations = LoadLocale("en");
        _targetTranslations = normalized == "en" ? _baseTranslations : LoadLocale(normalized);
    }

    public string T(string key, IReadOnlyDictionary<string, object?>? args = null)
    {
        var template = FindTemplate(key);
        if (args is null || args.Count == 0)
        {
            return template;
        }

        foreach (var pair in args)
        {
            var replacement = pair.Value?.ToString() ?? string.Empty;
            template = template.Replace($"{{{pair.Key}}}", replacement, StringComparison.OrdinalIgnoreCase);
        }

        return template;
    }

    public string T(string key, params (string Key, object? Value)[] tokens)
    {
        if (tokens.Length == 0)
        {
            return T(key);
        }

        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (tokenKey, value) in tokens)
        {
            dict[tokenKey] = value;
        }

        return T(key, dict);
    }

    private string FindTemplate(string key)
    {
        if (_targetTranslations.TryGetValue(key, out var translated))
        {
            return translated;
        }

        return _baseTranslations.TryGetValue(key, out var fallback) ? fallback : key;
    }

    private static IReadOnlyDictionary<string, string> LoadLocale(string name)
    {
        return Cache.GetOrAdd(name, static lang =>
        {
            var localesPath = Path.Combine(AppContext.BaseDirectory, "locales", $"{lang}.json");
            if (!File.Exists(localesPath))
            {
                return new Dictionary<string, string>();
            }

            try
            {
                using var stream = File.OpenRead(localesPath);
                var doc = JsonDocument.Parse(stream);
                var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    result[prop.Name] = prop.Value.GetString() ?? string.Empty;
                }
                return result;
            }
            catch
            {
                return new Dictionary<string, string>();
            }
        });
    }
}
