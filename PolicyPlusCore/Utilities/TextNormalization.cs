using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Text;

namespace PolicyPlusCore.Utilities;

internal static class TextNormalization
{
    public static string NormalizeStrict(string? input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;
        // NFKC + lower-invariant + strip control + condense spaces; keep basic punctuation
        var form = input!.Normalize(NormalizationForm.FormKC).ToLowerInvariant();
        var sb = new StringBuilder(form.Length);
        bool lastWasSpace = false;
        foreach (var ch in form)
        {
            if (char.IsControl(ch))
                continue;
            if (char.IsWhiteSpace(ch))
            {
                if (!lastWasSpace)
                {
                    sb.Append(' ');
                    lastWasSpace = true;
                }
                continue;
            }
            lastWasSpace = false;
            if (char.IsLetterOrDigit(ch) || " _-./\\:()[]{}+@#'\"".IndexOf(ch) >= 0)
            {
                sb.Append(ch);
            }
        }
        return sb.ToString().Trim();
    }

    public static string NormalizeLoose(string? input)
    {
        // Start from strict then apply kana normalization and simple accent/voicing folding
        var s = NormalizeStrict(input);
        if (s.Length == 0)
            return s;
        s = KanaUtils.FoldKana(s);
        s = RemoveLongVowelMarks(s);
        return s;
    }

    private static string RemoveLongVowelMarks(string s)
    {
        // Replace prolonged sound mark with nothing
        return s.Replace("ー", string.Empty).Replace("-", string.Empty);
    }

    public static string ToNGramTokens(string normalized, int minN = 2, int maxN = 3)
    {
        if (string.IsNullOrEmpty(normalized))
            return string.Empty;
        var tokens = new List<string>();
        var s = normalized.Replace(" ", string.Empty);
        for (int i = 0; i < s.Length; i++)
        {
            for (int n = minN; n <= maxN; n++)
            {
                if (i + n <= s.Length)
                    tokens.Add(s.Substring(i, n));
            }
        }
        return string.Join(' ', tokens.Distinct());
    }

    private static class KanaUtils
    {
        public static string FoldKana(string s)
        {
            // Convert hiragana to katakana, small kana to normal, and strip diacritics by mapping table.
            var sb = new StringBuilder(s.Length);
            foreach (var ch in s)
            {
                var mapped = MapKana(ch);
                if (mapped != '\0')
                    sb.Append(mapped);
                else
                    sb.Append(ch);
            }
            return sb.ToString();
        }

        private static char MapKana(char ch)
        {
            // Hiragana to Katakana
            if (ch >= '\u3041' && ch <= '\u3096')
            {
                int offset = '\u30A1' - '\u3041';
                return (char)(ch + offset);
            }
            // Small kana to regular (subset)
            return ch switch
            {
                'ｧ' => 'ア',
                'ｨ' => 'イ',
                'ｩ' => 'ウ',
                'ｪ' => 'エ',
                'ｫ' => 'オ',
                'ｬ' => 'ヤ',
                'ｭ' => 'ユ',
                'ｮ' => 'ヨ',
                'ｯ' => 'ツ',
                'ｰ' => '\0',
                _ => ch,
            };
        }
    }
}
