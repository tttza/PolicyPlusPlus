using System;
using System.Linq;
using PolicyPlus.Utilities;
using PolicyPlus.WinUI3.Services;

namespace PolicyPlus.WinUI3.Dialogs
{
    public static class FindByRegistryWinUI
    {
        public static bool WildcardMatch(string input, string pattern)
        {
            return StringMatch.WildcardMatch(input, pattern);
        }

        public static bool WildcardOrExact(string input, string pattern, bool allowSubstring)
        {
            return StringMatch.WildcardOrExact(input, pattern, allowSubstring);
        }

        public static bool SearchRegistry(PolicyPlusPolicy Policy, string keyName, string valName, bool allowSubstring = true)
        {
            var keyPat = keyName ?? string.Empty;
            var valPat = valName ?? string.Empty;
            var cached = RegistryReferenceCache.Get(Policy);

            // Value-name pattern first (cheap)
            if (!string.IsNullOrEmpty(valPat))
            {
                bool anyVal = cached.ValueNamesLower.Any(v => WildcardOrExact(v, valPat, allowSubstring));
                if (!anyVal) return false; // if a value pattern is specified but none match, bail out
            }

            if (string.IsNullOrEmpty(keyPat))
            {
                // Only value filter requested and it matched
                return !string.IsNullOrEmpty(valPat);
            }

            // Key path filter
            var pat = keyPat;
            if (pat.Contains("*") || pat.Contains("?"))
            {
                // wildcard full-path
                if (cached.KeyPathsLower.Any(k => WildcardMatch(k, pat))) return true;
                return false;
            }
            else if (pat.Contains(@"\\"))
            {
                // rooted path prefix
                if (cached.KeyPathsLower.Any(k => k.StartsWith(pat, StringComparison.InvariantCultureIgnoreCase))) return true;
                return false;
            }
            else
            {
                // single segment or substring
                foreach (var segs in cached.KeySegmentsLower)
                {
                    bool segMatch = segs.Any(s => string.Equals(s, pat, StringComparison.InvariantCultureIgnoreCase));
                    bool subMatch = allowSubstring && segs.Any(s => s.IndexOf(pat, StringComparison.InvariantCultureIgnoreCase) >= 0);
                    if (segMatch || subMatch) return true;
                }
                return false;
            }
        }

        public static bool SearchRegistryValueNameOnly(PolicyPlusPolicy policy, string valueNamePattern, bool allowSubstring = true)
        {
            var pat = valueNamePattern ?? string.Empty;
            if (string.IsNullOrWhiteSpace(pat))
                return false;

            var cached = RegistryReferenceCache.Get(policy);
            foreach (var v in cached.ValueNamesLower)
            {
                if (WildcardOrExact(v, pat, allowSubstring))
                    return true;
            }
            return false;
        }
    }
}
