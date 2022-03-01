using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace PolicyPlus
{
    public partial class DetailPolicyFormatted
    {
        public DetailPolicyFormatted()
        {
            InitializeComponent();
        }

        private PolicyPlusPolicy SelectedPolicy;

        private Dictionary<String, Dictionary<String, String>> wordDict = new Dictionary<string, Dictionary<string, string>>()
            {
                {"ja-jp",
                    new Dictionary<string, string>
                    {
                      {"User or computer", "コンピューターの構成 または ユーザーの構成" },
                      {"Computer", "コンピューターの構成" },
                      {"User", "ユーザーの構成"},
                      {"Administrative Templates", "管理用テンプレート"}
                    }
                }

            };

        private string TranslateWords(string keyword, string lang)
        {
            var convK = keyword;
            var dic = new Dictionary<string, string>();
            if (wordDict.TryGetValue(lang.ToLower(), out dic))
            {
                dic.TryGetValue(keyword, out convK);
            };
            return convK;
        }

        private void UpdatePolPathBox(PolicyPlusPolicy Policy, string languageCode)
        {
            List<string> GetParentNames(PolicyPlusCategory category, List<string> namesList = null)
            {
                if (namesList == null)
                {
                    namesList = new List<string>();
                }

                if (category.Parent is not null)
                {
                    namesList = GetParentNames(category.Parent, namesList);
                }
                namesList.Add(category.DisplayName);
                return namesList;
            }

            SelectedPolicy = Policy;
            NameTextbox.Text = Policy.DisplayName;
            IdTextbox.Text = Policy.UniqueID;
            DefinedTextbox.Text = Policy.RawPolicy.DefinedIn.SourceFile;

            if (languageCode == null) { languageCode = "en-US"; }
            switch (Policy.RawPolicy.Section)
            {
                case AdmxPolicySection.Both:
                    {
                        FormattedPolPathBox.Text = TranslateWords("User or computer", languageCode);
                        break;
                    }

                case AdmxPolicySection.Machine:
                    {
                        FormattedPolPathBox.Text = TranslateWords("Computer", languageCode);
                        break;
                    }

                case AdmxPolicySection.User:
                    { 
                        FormattedPolPathBox.Text = TranslateWords("User", languageCode);
                        break;
                    }
            }

            FormattedPolPathBox.Text += System.Environment.NewLine + "  + " + TranslateWords("Administrative Templates", languageCode);


            var parentNames = GetParentNames(Policy.Category);

            var depth_count = 2;
            foreach (var name in parentNames)
            {
                FormattedPolPathBox.Text += String.Concat(System.Environment.NewLine, new String(' ', 2*depth_count), "+ ", name);
                depth_count++;
            }
            FormattedPolPathBox.Text += String.Concat(System.Environment.NewLine, new String(' ', 2*depth_count), " ", Policy.DisplayName);

        }

        private void UpdateRegPathBox(PolicyPlusPolicy Policy, string languageCode)
        {

        }

        public void PresentDialog(PolicyPlusPolicy Policy, string languageCode)
        {
            UpdatePolPathBox(Policy, languageCode);
            ShowDialog();
        }

        private void SupportButton_Click(object sender, EventArgs e)
        {
            My.MyProject.Forms.DetailSupport.PresentDialog(SelectedPolicy.SupportedOn);
        }

        private void CategoryButton_Click(object sender, EventArgs e)
        {
            My.MyProject.Forms.DetailCategory.PresentDialog(SelectedPolicy.Category);
        }

        private void SectionLabel_Click(object sender, EventArgs e)
        {

        }

        private void SectionTextbox_TextChanged(object sender, EventArgs e)
        {

        }

        private void CopyToClipboard(object sender, EventArgs e)
        {
            var tag = (string)((Button)sender).Tag;
            if (tag == "Path")
            {
                Clipboard.SetText(FormattedPolPathBox.Text);
            }
        }

        private void DetailPolicyFormatted_Load(object sender, EventArgs e)
        {

        }
    }
}
