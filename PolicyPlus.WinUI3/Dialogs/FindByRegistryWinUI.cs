using System;
using System.Linq;
using PolicyPlus.Utilities;

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
            var affected = PolicyProcessing.GetReferencedRegistryValues(Policy);
            foreach (var rkvp in affected)
            {
                if (!string.IsNullOrEmpty(valPat))
                {
                    var v = (rkvp.Value ?? string.Empty);
                    if (!WildcardOrExact(v, valPat, allowSubstring))
                        continue;
                }

                if (!string.IsNullOrEmpty(keyPat))
                {
                    var keyLower = rkvp.Key.ToLowerInvariant();
                    var pat = keyPat;
                    if (pat.Contains("*") || pat.Contains("?"))
                    {
                        if (!WildcardMatch(keyLower, pat))
                            continue;
                    }
                    else if (pat.Contains(@"\"))
                    {
                        if (!keyLower.StartsWith(pat, StringComparison.InvariantCultureIgnoreCase))
                            continue;
                    }
                    else
                    {
                        bool segMatch = rkvp.Key.Split('\\').Any(part => part.Equals(pat, StringComparison.InvariantCultureIgnoreCase));
                        bool subMatch = allowSubstring && keyLower.IndexOf(pat, StringComparison.InvariantCultureIgnoreCase) >= 0;
                        if (!(segMatch || subMatch))
                            continue;
                    }
                }

                return true;
            }

            return false;
        }

        public static bool SearchRegistryValueNameOnly(PolicyPlusPolicy policy, string valueNamePattern, bool allowSubstring = true)
        {
            var pat = valueNamePattern ?? string.Empty;
            if (string.IsNullOrWhiteSpace(pat))
                return false;

            var affected = PolicyProcessing.GetReferencedRegistryValues(policy);
            foreach (var rkvp in affected)
            {
                var v = rkvp.Value ?? string.Empty;
                if (WildcardOrExact(v, pat, allowSubstring))
                    return true;
            }
            return false;
        }
    }
}
