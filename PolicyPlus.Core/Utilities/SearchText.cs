using System;
using System.Globalization;
using System.Text;

namespace PolicyPlus.Utilities
{
    public static class SearchText
    {
        // Optional toggle: when true and UI culture is Japanese, remove the prolonged sound mark
        // characters during normalization.
        public static bool RemoveProlongedSoundMark { get; set; } = false;

        // Normalize text for culture-aware search. For Japanese UI, unify width (NFKC),
        // convert Katakana to Hiragana, and lowercase to ignore case.
        public static string Normalize(string? s)
        {
            s ??= string.Empty;
            if (s.Length == 0) return string.Empty;

            // Normalize width and compatibility characters first
            string normalized = s.Normalize(NormalizationForm.FormKC);

            bool isJa = false;
            // If UI culture is Japanese, unify Kana type to Hiragana
            try { isJa = string.Equals(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName, "ja", StringComparison.OrdinalIgnoreCase); } catch { }

            if (isJa)
            {
                normalized = ToHiragana(normalized);
                if (RemoveProlongedSoundMark)
                {
                    normalized = StripProlongedMarks(normalized);
                }
            }

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

        private static string StripProlongedMarks(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            var sb = new StringBuilder(input.Length);
            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];
                // U+30FC: Katakana-Hiragana Prolonged Sound Mark
                // U+FF70: Halfwidth Katakana-Hiragana Prolonged Sound Mark
                if (c == '\u30FC' || c == '\uFF70') continue;
                sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
