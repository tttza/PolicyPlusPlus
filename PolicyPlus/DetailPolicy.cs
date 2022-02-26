using System;

namespace PolicyPlus
{
    public partial class DetailPolicy
    {
        public DetailPolicy()
        {
            InitializeComponent();
        }

        private PolicyPlusPolicy SelectedPolicy;

        public void PresentDialog(PolicyPlusPolicy Policy)
        {
            SelectedPolicy = Policy;
            NameTextbox.Text = Policy.DisplayName;
            IdTextbox.Text = Policy.UniqueID;
            DefinedTextbox.Text = Policy.RawPolicy.DefinedIn.SourceFile;
            DisplayCodeTextbox.Text = Policy.RawPolicy.DisplayCode;
            InfoCodeTextbox.Text = Policy.RawPolicy.ExplainCode;
            PresentCodeTextbox.Text = Policy.RawPolicy.PresentationID;
            switch (Policy.RawPolicy.Section)
            {
                case AdmxPolicySection.Both:
                    {
                        SectionTextbox.Text = "User or computer";
                        break;
                    }

                case AdmxPolicySection.Machine:
                    {
                        SectionTextbox.Text = "Computer";
                        break;
                    }

                case AdmxPolicySection.User:
                    {
                        SectionTextbox.Text = "User";
                        break;
                    }
            }

            SupportButton.Enabled = Policy.SupportedOn is object;
            if (Policy.SupportedOn is object)
            {
                SupportTextbox.Text = Policy.SupportedOn.DisplayName;
            }
            else if (!string.IsNullOrEmpty(Policy.RawPolicy.SupportedCode))
            {
                SupportTextbox.Text = "<missing: " + Policy.RawPolicy.SupportedCode + ">";
            }
            else
            {
                SupportTextbox.Text = "";
            }

            CategoryButton.Enabled = Policy.Category is object;
            if (Policy.Category is object)
            {
                CategoryTextbox.Text = Policy.Category.DisplayName;
            }
            else if (!string.IsNullOrEmpty(Policy.RawPolicy.CategoryID))
            {
                CategoryTextbox.Text = "<orphaned from " + Policy.RawPolicy.CategoryID + ">";
            }
            else
            {
                CategoryTextbox.Text = "<uncategorized>";
            }

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
    }
}