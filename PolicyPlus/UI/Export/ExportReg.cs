using PolicyPlus.Core.IO;

using System;
using System.Windows.Forms;

namespace PolicyPlus.UI.Export
{
    public partial class ExportReg
    {
        public ExportReg()
        {
            InitializeComponent();
        }

        private PolFile Source;

        public DialogResult PresentDialog(string Branch, PolFile Pol, bool IsUser)
        {
            Source = Pol;
            TextBranch.Text = Branch;
            TextRoot.Text = IsUser ? @"HKEY_CURRENT_USER\" : @"HKEY_LOCAL_MACHINE\";
            TextReg.Text = "";
            return ShowDialog();
        }

        private void ButtonBrowse_Click(object sender, EventArgs e)
        {
            using (var sfd = new SaveFileDialog())
            {
                sfd.Filter = "Registry scripts|*.reg";
                if (sfd.ShowDialog() == DialogResult.OK)
                    TextReg.Text = sfd.FileName;
            }
        }

        private void ExportReg_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
                DialogResult = DialogResult.Cancel;
        }

        private void ButtonExport_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(TextReg.Text))
            {
                MessageBox.Show("Please specify a filename and path for the exported REG.", "Policy Plus", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            var reg = new RegFile();
            reg.SetPrefix(TextRoot.Text);
            reg.SetSourceBranch(TextBranch.Text);
            try
            {
                Source.Apply(reg);
                reg.Save(TextReg.Text);
                MessageBox.Show("REG exported successfully.", "Policy Plus", MessageBoxButtons.OK, MessageBoxIcon.Information);
                DialogResult = DialogResult.OK;
            }
            catch (Exception)
            {
                MessageBox.Show("Failed to export REG!", "Policy Plus", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }
    }
}