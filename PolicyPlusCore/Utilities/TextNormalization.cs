using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Text;

namespace PolicyPlusCore.Utilities;

public static class TextNormalization
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

    // Performs the loose normalization steps assuming the input has already been normalized via NormalizeStrict.
    public static string NormalizeLooseFromStrict(string strictAlready)
    {
        if (string.IsNullOrEmpty(strictAlready))
            return string.Empty;
        var s = KanaUtils.FoldKana(strictAlready);
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
        var s = normalized.Replace(" ", string.Empty);
        // Use a set to avoid duplicate allocations and a StringBuilder to join efficiently.
        var set = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < s.Length; i++)
        {
            for (int n = minN; n <= maxN; n++)
            {
                int end = i + n;
                if (end <= s.Length)
                    set.Add(s.Substring(i, n));
            }
        }
        if (set.Count == 0)
            return string.Empty;
        // Sort for deterministic output; FTS5 does not care about order, but stable strings help caching and tests.
        var list = new List<string>(set);
        list.Sort(StringComparer.Ordinal);
        var sb = new StringBuilder(s.Length * (maxN - minN + 1));
        bool first = true;
        foreach (var tok in list)
        {
            if (!first)
                sb.Append(' ');
            sb.Append(tok);
            first = false;
        }
        return sb.ToString();
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
