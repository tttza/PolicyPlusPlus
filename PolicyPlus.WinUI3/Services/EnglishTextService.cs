using System;
using System.Collections.Concurrent;
using System.IO;
using PolicyPlus.Core.Admx;
using PolicyPlus.Core.Core;

namespace PolicyPlus.WinUI3.Services
{
    // Resolves English (en-US) display strings from ADML and composes localized + English text.
    internal static class EnglishTextService
    {
        private static readonly ConcurrentDictionary<string, AdmlFile?> _admlCache = new(StringComparer.OrdinalIgnoreCase);

        public static bool ShouldShowEnglish()
        {
            try
            {
                var s = SettingsService.Instance.LoadSettings();
                var lang = s.Language;
                bool nonEnglish = string.IsNullOrWhiteSpace(lang) ? !System.Globalization.CultureInfo.CurrentUICulture.Name.StartsWith("en", StringComparison.OrdinalIgnoreCase)
                                                                  : !lang.StartsWith("en", StringComparison.OrdinalIgnoreCase);
                // Default to ON for non-English UI so English is readily copyable
                return nonEnglish && (s.ShowEnglishNames ?? true);
            }
            catch { return true; }
        }

        public static string GetCompositePolicyName(PolicyPlusPolicy? p)
        {
            string local = p?.DisplayName ?? string.Empty;
            if (p is null) return local;
            if (!ShouldShowEnglish()) return local;
            string en = GetEnglishPolicyName(p);
            if (string.IsNullOrWhiteSpace(en) || string.Equals(en, local, StringComparison.Ordinal)) return local;
            return $"{local} ({en})";
        }

        public static string GetCompositeCategoryName(PolicyPlusCategory? c)
        {
            string local = c?.DisplayName ?? string.Empty;
            if (c is null) return local;
            if (!ShouldShowEnglish()) return local;
            string en = GetEnglishCategoryName(c);
            if (string.IsNullOrWhiteSpace(en) || string.Equals(en, local, StringComparison.Ordinal)) return local;
            return $"{local} ({en})";
        }

        public static string GetEnglishPolicyName(PolicyPlusPolicy p)
        {
            try
            {
                var raw = p?.RawPolicy;
                if (raw is null) return string.Empty;
                return ResolveEnglishString(raw.DisplayCode, raw.DefinedIn);
            }
            catch { return string.Empty; }
        }

        public static string GetEnglishCategoryName(PolicyPlusCategory c)
        {
            try
            {
                var raw = c?.RawCategory;
                if (raw is null) return string.Empty;
                return ResolveEnglishString(raw.DisplayCode, raw.DefinedIn);
            }
            catch { return string.Empty; }
        }

        public static string GetEnglishPolicyExplanation(PolicyPlusPolicy p)
        {
            try
            {
                var raw = p?.RawPolicy;
                if (raw is null) return string.Empty;
                return ResolveEnglishString(raw.ExplainCode, raw.DefinedIn);
            }
            catch { return string.Empty; }
        }

        private static string ResolveEnglishString(string? displayCode, AdmxFile admx)
        {
            if (string.IsNullOrEmpty(displayCode)) return string.Empty;
            // Literal string case
            if (!displayCode.StartsWith("$(string.", StringComparison.Ordinal))
                return displayCode;

            string id = displayCode.Substring(9, displayCode.Length - 10);
            var adml = LoadEnglishAdml(admx?.SourceFile ?? string.Empty);
            if (adml?.StringTable != null && adml.StringTable.TryGetValue(id, out var val))
                return val ?? string.Empty;
            return string.Empty;
        }

        private static AdmlFile? LoadEnglishAdml(string admxPath)
        {
            if (string.IsNullOrWhiteSpace(admxPath)) return null;
            return _admlCache.GetOrAdd(admxPath, p =>
            {
                try
                {
                    var fileTitle = Path.GetFileName(p);
                    var dir = Path.GetDirectoryName(p) ?? string.Empty;
                    var enUs = Path.ChangeExtension(Path.Combine(dir, "en-US", fileTitle), "adml");
                    if (File.Exists(enUs)) return AdmlFile.Load(enUs);

                    // Fallback: find any en-* folder
                    foreach (var sub in Directory.Exists(dir) ? Directory.EnumerateDirectories(dir) : Array.Empty<string>())
                    {
                        var name = Path.GetFileName(sub) ?? string.Empty;
                        if (name.StartsWith("en", StringComparison.OrdinalIgnoreCase))
                        {
                            var attempt = Path.ChangeExtension(Path.Combine(sub, fileTitle), "adml");
                            if (File.Exists(attempt)) return AdmlFile.Load(attempt);
                        }
                    }
                }
                catch { }
                return null;
            });
        }
    }
}
