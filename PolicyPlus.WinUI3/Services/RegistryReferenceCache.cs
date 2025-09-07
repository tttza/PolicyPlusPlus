using System;
using System.Collections.Concurrent;
using System.Linq;

using PolicyPlus.Core.Core;
using PolicyPlus.Core.Utilities;
using PolicyPlus.WinUI3.Logging; // logging

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
                var keysNorm = affected.Select(kv => SearchText.Normalize(kv.Key)).ToArray();
                var keySegs = affected
                    .Select(kv => (kv.Key ?? string.Empty).Split('\\'))
                    .Select(segs => segs.Select(s => SearchText.Normalize(s)).ToArray())
                    .ToArray();
                var valsNorm = affected.Select(kv => SearchText.Normalize(kv.Value)).ToArray();
                return new Cached
                {
                    KeyPathsLower = keysNorm,
                    KeySegmentsLower = keySegs,
                    ValueNamesLower = valsNorm,
                };
            }
            catch (Exception ex)
            {
#if DEBUG
                Log.Debug("RegRefCache", $"build failed policyId={(policy?.UniqueID ?? "(null)")}: {ex.GetType().Name} {ex.Message}");
#endif
                return new Cached();
            }
        }
    }
}
