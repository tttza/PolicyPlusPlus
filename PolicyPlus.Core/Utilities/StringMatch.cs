using System;

namespace PolicyPlus.Utilities
{
    public static class StringMatch
    {
        public static bool WildcardMatch(string input, string pattern)
        {
            input ??= string.Empty;
            pattern ??= string.Empty;
            // Case-insensitive wildcard match by normalizing to lowercase
            var s = input.ToLowerInvariant();
            var p = pattern.ToLowerInvariant();

            int i = 0, pi = 0, star = -1, mark = 0;
            while (i < s.Length)
            {
                if (pi < p.Length && (p[pi] == '?' || p[pi] == s[i])) { i++; pi++; continue; }
                if (pi < p.Length && p[pi] == '*') { star = pi++; mark = i; continue; }
                if (star != -1) { pi = star + 1; i = ++mark; continue; }
                return false;
            }
            while (pi < p.Length && p[pi] == '*') pi++;
            return pi == p.Length;
        }

        public static bool WildcardOrExact(string input, string pattern, bool allowSubstring)
        {
            if (string.IsNullOrEmpty(pattern)) return true;
            if (pattern.Contains('*') || pattern.Contains('?'))
                return WildcardMatch(input, pattern);
            if (string.Equals(input ?? string.Empty, pattern, StringComparison.InvariantCultureIgnoreCase))
                return true;
            return allowSubstring && (input ?? string.Empty).IndexOf(pattern, StringComparison.InvariantCultureIgnoreCase) >= 0;
        }
    }
}
