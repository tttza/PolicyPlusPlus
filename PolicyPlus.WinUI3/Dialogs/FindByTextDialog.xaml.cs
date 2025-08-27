using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PolicyPlus.WinUI3.Dialogs
{
    public sealed partial class FindByTextDialog : ContentDialog
    {
        public Func<PolicyPlusPolicy, bool>? Searcher { get; private set; }

        public FindByTextDialog()
        {
            this.InitializeComponent();
            this.PrimaryButtonClick += FindByTextDialog_PrimaryButtonClick;
        }

        private static bool WildcardMatch(string input, string pattern)
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

        private bool IsStringAHit(string searchedText, List<string> simpleWords, List<string> wildcards, List<string> quotedStrings)
        {
            string cleanupStr(string RawText)
            {
                if (RawText == null) return string.Empty;
                return new string(RawText.Trim().ToLowerInvariant().Where(c => !".,'\";/!(){}[] Å@".Contains(c)).ToArray());
            }
            string cleanText = cleanupStr(searchedText);
            var wordsInText = cleanText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return simpleWords.All(w => wordsInText.Contains(w))
                & wildcards.All(w => wordsInText.Any(wit => WildcardMatch(wit, w)))
                & quotedStrings.All(w => cleanText.Contains(" " + w + " ") | cleanText.StartsWith(w + " ") | cleanText.EndsWith(" " + w) | cleanText == w);
        }

        private (List<string> simple, List<string> wild, List<string> quoted) ParseQuery(string text, bool checkPartial)
        {
            var rawSplitted = (text ?? string.Empty).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var simpleWords = new List<string>();
            var wildcards = new List<string>();
            var quotedStrings = new List<string>();
            string partialQuotedString = "";

            string cleanupStr(string RawText)
            {
                return new string((RawText ?? string.Empty).Trim().ToLowerInvariant().Where(c => !".,'\";/!(){}[] Å@".Contains(c)).ToArray());
            }

            for (int n = 0; n <= rawSplitted.Length - 1; n++)
            {
                string curString = rawSplitted[n];
                if (checkPartial)
                {
                    curString = $"*{curString}*";
                }
                if (!string.IsNullOrEmpty(partialQuotedString))
                {
                    partialQuotedString += curString + " ";
                    if (curString.EndsWith("\""))
                    {
                        quotedStrings.Add(cleanupStr(partialQuotedString));
                        partialQuotedString = "";
                    }
                }
                else if (curString.StartsWith("\""))
                {
                    partialQuotedString = curString + " ";
                }
                else if (curString.Contains("*") | curString.Contains("?"))
                {
                    wildcards.Add(cleanupStr(curString));
                }
                else
                {
                    simpleWords.Add(cleanupStr(curString));
                }
            }

            return (simpleWords, wildcards, quotedStrings);
        }

        private void BuildSearcher()
        {
            string text = QueryText.Text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text))
            {
                Searcher = null;
                return;
            }
            string lowText = text.ToLowerInvariant();

            bool checkTitle = ChkTitle.IsChecked == true;
            bool checkDesc = ChkDescription.IsChecked == true;
            bool checkId = ChkId.IsChecked == true;
            bool checkPartial = ChkPartial.IsChecked == true;
            bool checkRegName = ChkRegName.IsChecked == true;

            if (!(checkTitle || checkDesc || checkId || checkRegName))
            {
                Searcher = null;
                return;
            }

            var (simple, wild, quoted) = ParseQuery(text, checkPartial);

            Searcher = (Policy) =>
            {
                if (checkTitle && IsStringAHit(Policy.DisplayName, simple, wild, quoted))
                    return true;
                if (checkDesc && IsStringAHit(Policy.DisplayExplanation ?? string.Empty, simple, wild, quoted))
                    return true;
                if (checkId && IsStringAHit(Policy.UniqueID.Split(':').ElementAtOrDefault(1) ?? string.Empty, simple, wild, quoted))
                    return true;
                if (checkRegName && FindByRegistryWinUI.SearchRegistry(Policy, string.Empty, lowText))
                    return true;
                return false;
            };
        }

        private void FindByTextDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            BuildSearcher();
            if (Searcher == null)
            {
                args.Cancel = true;
            }
        }
    }
}
