// Removed Microsoft.VisualBasic dependency by implementing custom wildcard matching
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace PolicyPlus.UI.Find
{
    public partial class FindByText
    {
        public FindByText()
        {
            InitializeComponent();
        }

        private Dictionary<string, string>[] CommentSources = new Dictionary<string, string>[] {};
        public Func<PolicyPlusPolicy, bool> Searcher;

        public DialogResult PresentDialog(params Dictionary<string, string>[] CommentDicts)
        {
            CommentSources = CommentDicts.Where(d => d is object).ToArray();
            return ShowDialog();
        }

        private void FindByText_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
                DialogResult = DialogResult.Cancel;
        }

        private void SearchButton_Click(object sender, EventArgs e)
        {
            string text = StringTextbox.Text;
            if (string.IsNullOrEmpty(text))
            {
                MessageBox.Show("Please enter search terms.", "Policy Plus", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }
            string lowText = text.ToLower();

            bool checkTitle = TitleCheckbox.Checked;
            bool checkDesc = DescriptionCheckbox.Checked;
            bool checkComment = CommentCheckbox.Checked;
            bool checkId = IdCheckbox.Checked;
            bool checkPartial = partialCheckbox.Checked;
            bool checkRegName = RegNameCheckbox.Checked;
            if (!(checkTitle | checkDesc | checkComment | checkId | checkRegName))
            {
                MessageBox.Show("At least one attribute must be searched. Check one of the boxes and try again.", "Policy Plus", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            Searcher = new Func<PolicyPlusPolicy, bool>((Policy) =>
            {
                string cleanupStr(string RawText)
                {
                    return new string(RawText.Trim().ToLowerInvariant().Where(c => !".,'\";/!(){}[] 　".Contains(c)).ToArray());
                }
                // Parse the query string for wildcards or quoted strings
                var rawSplitted = text.Split(new[]{' '}, StringSplitOptions.RemoveEmptyEntries);
                var simpleWords = new List<string>();
                var wildcards = new List<string>();
                var quotedStrings = new List<string>();
                string partialQuotedString = "";
                for (int n = 0, loopTo = rawSplitted.Length - 1; n <= loopTo; n++)
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
                // Do the searching
                bool isStringAHit(string SearchedText)
                {
                    string cleanText = cleanupStr(SearchedText);
                    var wordsInText = cleanText.Split(new[]{' '}, StringSplitOptions.RemoveEmptyEntries);
                    return simpleWords.All(w => wordsInText.Contains(w))
                        & wildcards.All(w => wordsInText.Any(wit => WildcardMatch(wit, w)))
                        & quotedStrings.All(w => cleanText.Contains(" " + w + " ") | cleanText.StartsWith(w + " ") | cleanText.EndsWith(" " + w) | cleanText == w); // Plain search terms / Wildcards / Quoted strings
                                                                                                                                                                                                                                                                                                                                         // Wildcards
                                                                                                                                                                                                                                                                                                                                         // Quoted strings
                };
                if (checkTitle)
                {
                    if (isStringAHit(Policy.DisplayName))
                        return true;
                }

                if (checkDesc)
                {
                    if (isStringAHit(Policy.DisplayExplanation))
                        return true;
                }

                if (checkComment)
                {
                    if (CommentSources.Any((Source) => Source.ContainsKey(Policy.UniqueID) && isStringAHit(Source[Policy.UniqueID])))
                        return true;
                }

                if (checkId)
                {
                    if (isStringAHit(Policy.UniqueID.Split(':')[1]))
                        return true;
                }

                if (checkRegName)
                {
                    if (FindByRegistry.SearchRegistry(Policy, "", lowText))
                        return true;
                }


                return false;
            });
            DialogResult = DialogResult.OK;
        }
        private static bool WildcardMatch(string input, string pattern)
        {
            int i = 0, p = 0, star = -1, mark = 0;
            while (i < input.Length)
            {
                if (p < pattern.Length && (pattern[p] == '?' || pattern[p] == input[i]))
                {
                    i++; p++; continue;
                }
                if (p < pattern.Length && pattern[p] == '*')
                {
                    star = p++;
                    mark = i;
                    continue;
                }
                if (star != -1)
                {
                    p = star + 1;
                    i = ++mark;
                    continue;
                }
                return false;
            }
            while (p < pattern.Length && pattern[p] == '*') p++;
            return p == pattern.Length;
        }
    }
}