using System;
using System.Linq;

namespace PolicyPlus.WinUI3.Dialogs
{
    public static class FindByRegistryWinUI
    {
        public static bool WildcardMatch(string input, string pattern)
        {
            int i = 0, p = 0, star = -1, mark = 0;
            while (i < input.Length)
            {
                if (p < pattern.Length && (pattern[p] == '?' || pattern[p] == input[i])) { i++; p++; continue; }
                if (p < pattern.Length && pattern[p] == '*') { star = p++; mark = i; continue; }
                if (star != -1) { p = star + 1; i = ++mark; continue; }
                return false;
            }
            while (p < pattern.Length && pattern[p] == '*') p++;
            return p == pattern.Length;
        }

        public static bool WildcardOrExact(string input, string pattern)
        {
            if (pattern.Contains('*') || pattern.Contains('?'))
                return WildcardMatch(input, pattern);
            return input.Equals(pattern, StringComparison.InvariantCultureIgnoreCase);
        }

        public static bool SearchRegistry(PolicyPlusPolicy Policy, string keyName, string valName)
        {
            var affected = PolicyProcessing.GetReferencedRegistryValues(Policy);
            foreach (var rkvp in affected)
            {
                if (!string.IsNullOrEmpty(valName))
                {
                    if (!WildcardOrExact(rkvp.Value.ToLowerInvariant(), valName))
                        continue;
                }

                if (!string.IsNullOrEmpty(keyName))
                {
                    if (keyName.Contains("*") | keyName.Contains("?"))
                    {
                        if (!WildcardMatch(rkvp.Key.ToLowerInvariant(), keyName))
                            continue;
                    }
                    else if (keyName.Contains(@"\"))
                    {
                        if (!rkvp.Key.StartsWith(keyName, StringComparison.InvariantCultureIgnoreCase))
                            continue;
                    }
                    else if (!rkvp.Key.Split('\\').Any(part => part.Equals(keyName, StringComparison.InvariantCultureIgnoreCase)))
                        continue;
                }

                return true;
            }

            return false;
        }
    }
}
