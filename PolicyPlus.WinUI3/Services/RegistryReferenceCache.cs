using System;
using System.Collections.Concurrent;
using System.Linq;
using PolicyPlus; // for PolicyPlusPolicy, PolicyProcessing

namespace PolicyPlus.WinUI3.Services
{
    internal static class RegistryReferenceCache
    {
        internal sealed class Cached
        {
            public string[] KeyPathsLower { get; init; } = Array.Empty<string>();
            public string[][] KeySegmentsLower { get; init; } = Array.Empty<string[]>();
            public string[] ValueNamesLower { get; init; } = Array.Empty<string>();
        }

        private static readonly ConcurrentDictionary<string, Cached> s_cache = new(StringComparer.OrdinalIgnoreCase);

        public static void Clear() => s_cache.Clear();

        public static Cached Get(PolicyPlusPolicy policy)
        {
            if (policy == null) return new Cached();
            var id = policy.UniqueID ?? string.Empty;
            return s_cache.GetOrAdd(id, _ => Build(policy));
        }

        private static Cached Build(PolicyPlusPolicy policy)
        {
            try
            {
                var affected = PolicyProcessing.GetReferencedRegistryValues(policy);
                var keysLower = affected.Select(kv => (kv.Key ?? string.Empty).ToLowerInvariant()).ToArray();
                var keySegs = affected
                    .Select(kv => (kv.Key ?? string.Empty).Split('\\'))
                    .Select(segs => segs.Select(s => (s ?? string.Empty).ToLowerInvariant()).ToArray())
                    .ToArray();
                var valsLower = affected.Select(kv => (kv.Value ?? string.Empty).ToLowerInvariant()).ToArray();
                return new Cached
                {
                    KeyPathsLower = keysLower,
                    KeySegmentsLower = keySegs,
                    ValueNamesLower = valsLower,
                };
            }
            catch
            {
                return new Cached();
            }
        }
    }
}
