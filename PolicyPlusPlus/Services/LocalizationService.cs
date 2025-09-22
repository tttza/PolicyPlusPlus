using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace PolicyPlusPlus.Services
{
    internal sealed class LocalizationService
    {
        private static readonly Lazy<LocalizationService> _instance = new(() =>
            new LocalizationService()
        );
        public static LocalizationService Instance => _instance.Value;

        private readonly Dictionary<string, Dictionary<string, string>> _map = new(
            StringComparer.Ordinal
        );

        // Maps a language code to a preferred region variant when only language is provided.
        private static readonly IReadOnlyDictionary<string, string> s_defaultRegionMap =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["en"] = "en-US",
                ["ja"] = "ja-JP",
                ["de"] = "de-DE",
                ["fr"] = "fr-FR",
                ["es"] = "es-ES",
                ["it"] = "it-IT",
                ["ko"] = "ko-KR",
                ["ru"] = "ru-RU",
                ["pt"] = "pt-PT",
                ["zh"] = "zh-CN",
                ["nb"] = "nb-NO",
                ["nl"] = "nl-NL",
                ["pl"] = "pl-PL",
                ["sv"] = "sv-SE",
                ["tr"] = "tr-TR",
                ["fi"] = "fi-FI",
                ["hu"] = "hu-HU",
                ["el"] = "el-GR",
                ["cs"] = "cs-CZ",
                ["da"] = "da-DK",
            };

        private LocalizationService()
        {
            // Attempt to load translations from the app's resources folder.
            try
            {
                var baseDir = AppContext.BaseDirectory;
                var nestedPath = Path.Combine(
                    baseDir,
                    "Resources",
                    "Localization",
                    "PathTranslations.json"
                );
                var flatPath = Path.Combine(baseDir, "PathTranslations.json");
                var resourcePath = File.Exists(nestedPath) ? nestedPath : flatPath;
                if (File.Exists(resourcePath))
                {
                    using var fs = File.OpenRead(resourcePath);
                    using var doc = JsonDocument.Parse(fs);
                    if (doc.RootElement.TryGetProperty("translations", out var translations))
                    {
                        foreach (var prop in translations.EnumerateObject())
                        {
                            var key = prop.Name;
                            var value = prop.Value;
                            var langMap = new Dictionary<string, string>(
                                StringComparer.OrdinalIgnoreCase
                            );
                            foreach (var lang in value.EnumerateObject())
                            {
                                langMap[lang.Name] = lang.Value.GetString() ?? string.Empty;
                            }
                            _map[key] = langMap;
                        }
                    }
                }
            }
            catch
            {
                // Intentionally swallow: localization is optional; fall back to English keys
            }
        }

        // Translates a static key to the requested culture. If no translation found, returns the key itself.
        public string Translate(string key, string? cultureOrNull)
        {
            if (string.IsNullOrEmpty(key))
                return string.Empty;

            if (!_map.TryGetValue(key, out var langs))
                return key;

            var culture = cultureOrNull;
            if (string.IsNullOrWhiteSpace(culture))
                culture = CultureInfo.CurrentUICulture.Name;

            // Try full culture (e.g., ja-JP)
            if (!string.IsNullOrWhiteSpace(culture))
            {
                if (langs.TryGetValue(culture, out var v) && !string.IsNullOrEmpty(v))
                    return v;

                try
                {
                    var ci = new CultureInfo(culture);
                    var parent = ci.TwoLetterISOLanguageName;
                    // If only a language is provided or region is missing, try a preferred region variant first.
                    if (!string.IsNullOrWhiteSpace(parent))
                    {
                        if (s_defaultRegionMap.TryGetValue(parent, out var preferred))
                        {
                            if (
                                langs.TryGetValue(preferred, out var pv)
                                && !string.IsNullOrEmpty(pv)
                            )
                                return pv;
                        }
                        // As a last attempt for this branch, try language-only key if present in file (older files)
                        if (langs.TryGetValue(parent, out var p) && !string.IsNullOrEmpty(p))
                            return p;
                    }
                }
                catch
                {
                    // ignore bad culture
                }
            }

            // Fallback to English if present
            if (langs.TryGetValue("en-US", out var enUs) && !string.IsNullOrEmpty(enUs))
                return enUs;
            if (langs.TryGetValue("en", out var en) && !string.IsNullOrEmpty(en))
                return en;

            // Ultimately fallback to key
            return key;
        }
    }
}
