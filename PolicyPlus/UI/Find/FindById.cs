using PolicyPlusCore.Admx;
using PolicyPlusCore.Core;

using System;
using System.Drawing;
using System.Windows.Forms;

namespace PolicyPlus.UI.Find
{
    public partial class FindById
    {
        public FindById()
        {
            InitializeComponent();
        }

        public AdmxBundle AdmxWorkspace;
        public PolicyPlusPolicy SelectedPolicy;
        public PolicyPlusCategory SelectedCategory;
        public PolicyPlusProduct SelectedProduct;
        public PolicyPlusSupport SelectedSupport;
        public AdmxPolicySection SelectedSection; // Specifies the section for policies
        private Image CategoryImage;
        private Image PolicyImage;
        private Image ProductImage;
        private Image SupportImage;
        private Image NotFoundImage;
        private Image BlankImage;

        private void FindById_Load(object sender, EventArgs e)
        {
            CategoryImage = AppForms.Main.PolicyIcons.Images[0];
            PolicyImage = AppForms.Main.PolicyIcons.Images[4];
            ProductImage = AppForms.Main.PolicyIcons.Images[10];
            SupportImage = AppForms.Main.PolicyIcons.Images[11];
            NotFoundImage = AppForms.Main.PolicyIcons.Images[8];
            BlankImage = AppForms.Main.PolicyIcons.Images[9];
        }

        private void IdTextbox_TextChanged(object sender, EventArgs e)
        {
            if (AdmxWorkspace is null)
                return; // Wait until actually shown
            SelectedPolicy = null;
            SelectedCategory = null;
            SelectedProduct = null;
            SelectedSupport = null;
            string id = IdTextbox.Text.Trim();
            if (AdmxWorkspace.FlatCategories.ContainsKey(id))
            {
                StatusImage.Image = CategoryImage;
                SelectedCategory = AdmxWorkspace.FlatCategories[id];
            }
            else if (AdmxWorkspace.FlatProducts.ContainsKey(id))
            {
                StatusImage.Image = ProductImage;
                SelectedProduct = AdmxWorkspace.FlatProducts[id];
            }
            else if (AdmxWorkspace.SupportDefinitions.ContainsKey(id))
            {
                StatusImage.Image = SupportImage;
                SelectedSupport = AdmxWorkspace.SupportDefinitions[id];
            }
            else // Check for a policy
            {
                var policyAndSection = id.Split(new[]{'@'}, 2);
                string policyId = policyAndSection[0]; // Cut off the section override
                if (AdmxWorkspace.Policies.ContainsKey(policyId))
                {
                    StatusImage.Image = PolicyImage;
                    SelectedPolicy = AdmxWorkspace.Policies[policyId];
                    if (policyAndSection.Length == 2 && policyAndSection[1].Length == 1 && "UC".Contains(policyAndSection[1]))
                    {
                        SelectedSection = policyAndSection[1] == "U" ? AdmxPolicySection.User : AdmxPolicySection.Machine;
                    }
                    else
                    {
                        SelectedSection = AdmxPolicySection.Both;
                    }
                }
                else
                {
                    StatusImage.Image = string.IsNullOrEmpty(id) ? BlankImage : NotFoundImage;
                }
            }
        }

        private void FindById_Shown(object sender, EventArgs e)
        {
            if (IdTextbox.Text == " ")
                IdTextbox.Text = ""; // It's set to a single space in the designer
            IdTextbox.Focus();
            IdTextbox.SelectAll();
        }

        private void GoButton_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK; // Close
        }

        private void FindById_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
                DialogResult = DialogResult.Cancel;
        }
    }
}