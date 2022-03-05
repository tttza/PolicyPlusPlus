using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace PolicyPlus
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
                Interaction.MsgBox("Please enter search terms.", MsgBoxStyle.Exclamation);
                return;
            }

            bool checkTitle = TitleCheckbox.Checked;
            bool checkDesc = DescriptionCheckbox.Checked;
            bool checkComment = CommentCheckbox.Checked;
            bool checkId = IdCheckbox.Checked;
            if (!(checkTitle | checkDesc | checkComment | checkId))
            {
                Interaction.MsgBox("At least one attribute must be searched. Check one of the boxes and try again.", MsgBoxStyle.Exclamation);
                return;
            }

            Searcher = new Func<PolicyPlusPolicy, bool>((Policy) =>
            {
                string cleanupStr(string RawText)
                {
                    return new string(Strings.Trim(RawText.ToLowerInvariant()).Where(c => !".,'\";/!(){}[]".Contains(Conversions.ToString(c))).ToArray());
                }
                // Parse the query string for wildcards or quoted strings
                var rawSplitted = Strings.Split(text);
                var simpleWords = new List<string>();
                var wildcards = new List<string>();
                var quotedStrings = new List<string>();
                string partialQuotedString = "";
                for (int n = 0, loopTo = rawSplitted.Length - 1; n <= loopTo; n++)
                {
                    string curString = rawSplitted[n];
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
                    var wordsInText = Strings.Split(cleanText);
                    return simpleWords.All(w => wordsInText.Contains(w)) & wildcards.All(w => wordsInText.Any(wit => LikeOperator.LikeString(wit, w, CompareMethod.Binary))) & quotedStrings.All(w => cleanText.Contains(" " + w + " ") | cleanText.StartsWith(w + " ") | cleanText.EndsWith(" " + w) | (cleanText ?? "") == (w ?? "")); // Plain search terms
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

                return false;
            });
            DialogResult = DialogResult.OK;
        }
    }
}