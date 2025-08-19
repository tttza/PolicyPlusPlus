using Microsoft.Win32;
using System.Windows.Forms;

namespace PolicyPlus.UI.PolicyDetail
{
    public partial class EditPolValue
    {
        public EditPolValue()
        {
            InitializeComponent();
        }

        public RegistryValueKind SelectedKind;
        public string ChosenName;

        private void EditPolValueType_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
                DialogResult = DialogResult.Cancel;
        }

        public DialogResult PresentDialog()
        {
            TextName.Text = "";
            ComboKind.SelectedIndex = 0;
            var dlgRes = ShowDialog();
            if (dlgRes == DialogResult.OK)
            {
                switch (ComboKind.SelectedIndex)
                {
                    case 0:
                        {
                            SelectedKind = RegistryValueKind.String;
                            break;
                        }

                    case 1:
                        {
                            SelectedKind = RegistryValueKind.ExpandString;
                            break;
                        }

                    case 2:
                        {
                            SelectedKind = RegistryValueKind.MultiString;
                            break;
                        }

                    case 3:
                        {
                            SelectedKind = RegistryValueKind.DWord;
                            break;
                        }

                    case 4:
                        {
                            SelectedKind = RegistryValueKind.QWord;
                            break;
                        }
                }

                ChosenName = TextName.Text;
            }

            return dlgRes;
        }
    }
}