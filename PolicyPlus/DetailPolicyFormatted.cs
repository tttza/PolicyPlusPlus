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

        public void PresentDialog(PolicyPlusPolicy Policy, string languageCode)
        {

            List<string> GetParentNames(PolicyPlusCategory category, List<string> namesList = null)
            {
                if (namesList == null)
                {
                    namesList = new List<string>();
                }
                namesList.Add(category.DisplayName);
                if (category.Parent is not null)
                {
                    namesList = GetParentNames(category.Parent, namesList);
                }
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
                        if (languageCode == "ja-JP")
                        {
                            FormattedPathBox.Text = "コンピューターの構成 または ユーザーの構成";
                        }
                        else
                        {
                            FormattedPathBox.Text = "User or computer";
                        }
                        break;
                    }

                case AdmxPolicySection.Machine:
                    {
                        if (languageCode == "ja-JP")
                        {
                            FormattedPathBox.Text = "コンピューターの構成";
                        }
                        else
                        {
                            FormattedPathBox.Text = "Computer";
                        }
                        break;
                    }

                case AdmxPolicySection.User:
                    {
                        if (languageCode == "ja-JP")
                        {
                            FormattedPathBox.Text = "ユーザーの構成";
                        }
                        else
                        {
                            FormattedPathBox.Text = "User";
                        }
                        break;
                    }
            }
            if (languageCode == "ja-JP")
            {
                FormattedPathBox.Text += System.Environment.NewLine + "  + 管理者用テンプレート";
            }
            else
            {
                FormattedPathBox.Text += System.Environment.NewLine + "  + Administrative Templates";
            }

                var parentNames = GetParentNames(Policy.Category);

            var depth_count = 2;
            foreach (var name in parentNames)
            {
                FormattedPathBox.Text += String.Concat(System.Environment.NewLine, new String(' ', 2*depth_count), "+ ", name);
                depth_count++;
            }
            FormattedPathBox.Text += String.Concat(System.Environment.NewLine, new String(' ', 2*depth_count), " ", Policy.DisplayName);

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
                Clipboard.SetText(FormattedPathBox.Text);
            }
        }
    }
}