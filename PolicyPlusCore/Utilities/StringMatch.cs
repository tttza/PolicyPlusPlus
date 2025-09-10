using System;

namespace PolicyPlusCore.Utilities
{
    public static class StringMatch
    {
        public static bool WildcardMatch(string input, string pattern)
        {
            input ??= string.Empty;
            pattern ??= string.Empty;
            // Normalize both sides for culture-aware, case-insensitive match (Japanese-friendly)
            var s = SearchText.Normalize(input);
            var p = SearchText.Normalize(pattern);

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

            var a = SearchText.Normalize(input);
            var b = SearchText.Normalize(pattern);
            if (string.Equals(a, b, StringComparison.Ordinal))
                return true;
            return allowSubstring && a.IndexOf(b, StringComparison.Ordinal) >= 0;
        }
    }
}
