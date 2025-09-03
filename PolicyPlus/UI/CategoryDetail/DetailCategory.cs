using PolicyPlus.Core.Core;

using System;

namespace PolicyPlus.UI.CategoryDetail
{
    public partial class DetailCategory
    {
        public DetailCategory()
        {
            InitializeComponent();
        }

        private PolicyPlusCategory SelectedCategory;

        public void PresentDialog(PolicyPlusCategory Category)
        {
            PrepareDialog(Category);
            ShowDialog();
        }

        private void PrepareDialog(PolicyPlusCategory Category)
        {
            SelectedCategory = Category;
            NameTextbox.Text = Category.DisplayName;
            IdTextbox.Text = Category.UniqueID;
            DefinedTextbox.Text = Category.RawCategory.DefinedIn.SourceFile;
            DisplayCodeTextbox.Text = Category.RawCategory.DisplayCode;
            InfoCodeTextbox.Text = Category.RawCategory.ExplainCode;
            ParentButton.Enabled = Category.Parent is object;
            if (Category.Parent is object)
            {
                ParentTextbox.Text = Category.Parent.DisplayName;
            }
            else if (!string.IsNullOrEmpty(Category.RawCategory.ParentID))
            {
                ParentTextbox.Text = "<orphaned from " + Category.RawCategory.ParentID + ">";
            }
            else
            {
                ParentTextbox.Text = "";
            }
        }

        private void ParentButton_Click(object sender, EventArgs e)
        {
            PrepareDialog(SelectedCategory.Parent);
        }
    }
}