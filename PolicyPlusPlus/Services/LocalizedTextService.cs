using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Linq;
using PolicyPlusCore.Admx;
using PolicyPlusCore.Core;

namespace PolicyPlusPlus.Services
{
    internal static class LocalizedTextService
    {
        private static readonly ConcurrentDictionary<(string admxPath, string lang), AdmlFile?> _cache = new();

        private static AdmlFile? LoadAdml(string admxPath, string lang)
        {
            if (string.IsNullOrWhiteSpace(admxPath) || string.IsNullOrWhiteSpace(lang)) return null;
            return _cache.GetOrAdd((admxPath, lang), k =>
            {
                try
                {
                    var fileTitle = Path.GetFileName(k.admxPath);
                    var dir = Path.GetDirectoryName(k.admxPath) ?? string.Empty;
                    var candidate = Path.ChangeExtension(Path.Combine(dir, k.lang, fileTitle), "adml");
                    if (File.Exists(candidate)) return AdmlFile.Load(candidate);

                    // Try exact (case-insensitive) folder match
                    foreach (var sub in Directory.Exists(dir) ? Directory.EnumerateDirectories(dir) : Array.Empty<string>())
                    {
                        var name = Path.GetFileName(sub) ?? string.Empty;
                        if (string.Equals(name, k.lang, StringComparison.OrdinalIgnoreCase))
                        {
                            var attempt = Path.ChangeExtension(Path.Combine(sub, fileTitle), "adml");
                            if (File.Exists(attempt)) return AdmlFile.Load(attempt);
                        }
                    }

                    var langPrefix = k.lang.Split('-')[0];
                    foreach (var sub in Directory.Exists(dir) ? Directory.EnumerateDirectories(dir) : Array.Empty<string>())
                    {
                        var name = Path.GetFileName(sub) ?? string.Empty;
                        if (name.StartsWith(langPrefix, StringComparison.OrdinalIgnoreCase))
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

        private static AdmlFile? LoadAdmlForPrefix(string admxPath, string lang, string prefix)
        {
            if (string.IsNullOrWhiteSpace(admxPath) || string.IsNullOrWhiteSpace(prefix)) return null;
            try
            {
                var dir = Path.GetDirectoryName(admxPath) ?? string.Empty;
                var candidate = Path.ChangeExtension(Path.Combine(dir, lang, prefix + ".adml"), "adml");
                if (File.Exists(candidate)) return AdmlFile.Load(candidate);
                // Try culture-near folders
                var langPrefix = lang.Split('-')[0];
                foreach (var sub in Directory.Exists(dir) ? Directory.EnumerateDirectories(dir) : Array.Empty<string>())
                {
                    var name = Path.GetFileName(sub) ?? string.Empty;
                    if (name.StartsWith(langPrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        var attempt = Path.ChangeExtension(Path.Combine(sub, prefix + ".adml"), "adml");
                        if (File.Exists(attempt)) return AdmlFile.Load(attempt);
                    }
                }
            }
            catch { }
            return null;
        }

        public static string ResolveString(string? displayCode, AdmxFile admx, string lang)
        {
            if (string.IsNullOrEmpty(displayCode)) return string.Empty;

            string id = displayCode;
            AdmlFile? adml = null;

            if (displayCode.StartsWith("$(string.", StringComparison.Ordinal))
            {
                id = displayCode.Substring(9, displayCode.Length - 10);
                adml = LoadAdml(admx?.SourceFile ?? string.Empty, lang);
            }
            else if (displayCode.Contains(":"))
            {
                var parts = displayCode.Split(new[] { ':' }, 2);
                var prefix = parts[0];
                id = parts[1];
                adml = LoadAdmlForPrefix(admx?.SourceFile ?? string.Empty, lang, prefix) ?? LoadAdml(admx?.SourceFile ?? string.Empty, lang);
            }
            else
            {
                adml = LoadAdml(admx?.SourceFile ?? string.Empty, lang);
            }

            if (adml?.StringTable != null && adml.StringTable.TryGetValue(id, out var val) && !string.IsNullOrEmpty(val))
                return val!;

            // Fallback: if original was a literal (not $(string.)), return it unchanged
            if (!displayCode.StartsWith("$(string.", StringComparison.Ordinal))
                return displayCode;
            return string.Empty;
        }

        public static string GetPolicyNameIn(PolicyPlusPolicy p, string lang)
        {
            try
            {
                var raw = p?.RawPolicy; if (raw is null) return string.Empty;
                return ResolveString(raw.DisplayCode, raw.DefinedIn, lang);
            }
            catch { return string.Empty; }
        }

        public static string GetCategoryNameIn(PolicyPlusCategory c, string lang)
        {
            try
            {
                var raw = c?.RawCategory; if (raw is null) return string.Empty;
                return ResolveString(raw.DisplayCode, raw.DefinedIn, lang);
            }
            catch { return string.Empty; }
        }

        public static string GetPolicyExplanationIn(PolicyPlusPolicy p, string lang)
        {
            try
            {
                var raw = p?.RawPolicy; if (raw is null) return string.Empty;
                return ResolveString(raw.ExplainCode, raw.DefinedIn, lang);
            }
            catch { return string.Empty; }
        }

        public static string GetSupportedDisplayIn(PolicyPlusPolicy p, string lang)
        {
            try
            {
                var raw = p?.RawPolicy; if (raw is null) return string.Empty;
                return ResolveString(raw.SupportedCode, raw.DefinedIn, lang);
            }
            catch { return string.Empty; }
        }

        public static Presentation? GetPresentationIn(PolicyPlusPolicy p, string lang)
        {
            try
            {
                var raw = p.RawPolicy;
                if (raw == null) return null;
                var src = raw.DefinedIn != null ? raw.DefinedIn.SourceFile : string.Empty;
                var adml = LoadAdml(src, lang);
                if (adml?.PresentationTable == null || adml.PresentationTable.Count == 0) return null;

                // Primary key: PresentationID from ADMX
                var presId = raw.PresentationID;
                if (!string.IsNullOrEmpty(presId))
                {
                    if (adml.PresentationTable.TryGetValue(presId, out var presExact))
                        return presExact;
                    // Case-insensitive match
                    var ci = adml.PresentationTable.FirstOrDefault(kv => string.Equals(kv.Key, presId, StringComparison.OrdinalIgnoreCase));
                    if (!string.IsNullOrEmpty(ci.Key)) return ci.Value;
                }

                // Secondary: current presentation name
                var currentName = p.Presentation?.Name;
                if (!string.IsNullOrEmpty(currentName))
                {
                    if (adml.PresentationTable.TryGetValue(currentName, out var presByName))
                        return presByName;
                    var ci2 = adml.PresentationTable.FirstOrDefault(kv => string.Equals(kv.Key, currentName, StringComparison.OrdinalIgnoreCase));
                    if (!string.IsNullOrEmpty(ci2.Key)) return ci2.Value;
                }

                return null;
            }
            catch { return null; }
        }
    }
}
