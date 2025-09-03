using System;
using System.Globalization;
using System.Text;

namespace PolicyPlus.Core.Utilities
{
    public static class SearchText
    {
        // Normalize text for culture-aware search. For Japanese UI, unify width (NFKC),
        // convert Katakana to Hiragana, and lowercase to ignore case.
        public static string Normalize(string? s)
        {
            s ??= string.Empty;
            // Fast path for empty
            if (s.Length == 0) return string.Empty;

            // Normalize width and compatibility characters first
            string normalized = s.Normalize(NormalizationForm.FormKC);

            // If UI culture is Japanese, unify Kana type to Hiragana
            try
            {
                var two = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
                if (string.Equals(two, "ja", StringComparison.OrdinalIgnoreCase))
                {
                    normalized = ToHiragana(normalized);
                }
            }
            catch { }

            // Lowercase for case-insensitive matching. Invariant is fine after NFKC.
            try { normalized = normalized.ToLowerInvariant(); } catch { }
            return normalized;
        }

        private static string ToHiragana(string input)
        {
            // Katakana range U+30A1..U+30F6 maps to Hiragana by -0x60 offset
            // Keep other chars as-is (including prolonged sound mark U+30FC)
            var sb = new StringBuilder(input.Length);
            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];
                if (c >= '\u30A1' && c <= '\u30F6')
                {
                    sb.Append((char)(c - 0x60));
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }
    }
}
