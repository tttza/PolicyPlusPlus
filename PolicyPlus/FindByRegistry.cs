using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;
using System;
using System.Linq;
using System.Windows.Forms;

namespace PolicyPlus
{
    public partial class FindByRegistry
    {
        public FindByRegistry()
        {
            InitializeComponent();
        }

        public Func<PolicyPlusPolicy, bool> Searcher;

        private void FindByRegistry_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
                DialogResult = DialogResult.Cancel;
        }

        public static bool SearchRegistry(PolicyPlusPolicy Policy, string keyName, string valName) {
            
            var affected = PolicyProcessing.GetReferencedRegistryValues(Policy);
            foreach (var rkvp in affected)
            {
                if (!string.IsNullOrEmpty(valName))
                {
                    if (!LikeOperator.LikeString(rkvp.Value.ToLowerInvariant(), valName, CompareMethod.Binary))
                        continue;
                }

                if (!string.IsNullOrEmpty(keyName))
                {
                    if (keyName.Contains("*") | keyName.Contains("?")) // Wildcard path
                    {
                        if (!LikeOperator.LikeString(rkvp.Key.ToLowerInvariant(), keyName, CompareMethod.Binary))
                            continue;
                    }
                    else if (keyName.Contains(@"\")) // Path root
                    {
                        if (!rkvp.Key.StartsWith(keyName, StringComparison.InvariantCultureIgnoreCase))
                            continue;
                    }
                    else if (!Strings.Split(rkvp.Key, @"\").Any(part => part.Equals(keyName, StringComparison.InvariantCultureIgnoreCase))) // One path component
                        continue;
                }

                return true;
            }

            return false;
            
        }

        private void SearchButton_Click(object sender, EventArgs e)
        {
            string keyName = KeyTextbox.Text.ToLowerInvariant();
            string valName = ValueTextbox.Text.ToLowerInvariant();
            if (string.IsNullOrEmpty(keyName) & string.IsNullOrEmpty(valName))
            {
                Interaction.MsgBox("Please enter search terms.", MsgBoxStyle.Exclamation);
                return;
            }

            if (new[] { @"HKLM\", @"HKCU\", @"HKEY_LOCAL_MACHINE\", @"HKEY_CURRENT_USER\" }.Any(bad => keyName.StartsWith(bad, StringComparison.InvariantCultureIgnoreCase)))
            {
                Interaction.MsgBox("Policies' root keys are determined only by their section. Remove the root key from the search terms and try again.", MsgBoxStyle.Exclamation);
                return;
            }

            Searcher = new Func<PolicyPlusPolicy, bool>((Policy) =>
            {
                return SearchRegistry(Policy, keyName, valName);
            });
            DialogResult = DialogResult.OK;
        }
    }
}