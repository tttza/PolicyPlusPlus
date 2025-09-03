using PolicyPlus.Core.Core;

using System;
using System.Linq;
using System.Windows.Forms;

namespace PolicyPlus.UI.Find
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
                    if (!WildcardOrExact(rkvp.Value.ToLowerInvariant(), valName))
                        continue;
                }

                if (!string.IsNullOrEmpty(keyName))
                {
                    if (keyName.Contains("*") | keyName.Contains("?")) // Wildcard path
                    {
                        if (!WildcardMatch(rkvp.Key.ToLowerInvariant(), keyName))
                            continue;
                    }
                    else if (keyName.Contains(@"\")) // Path root
                    {
                        if (!rkvp.Key.StartsWith(keyName, StringComparison.InvariantCultureIgnoreCase))
                            continue;
                    }
                    else if (!rkvp.Key.Split('\\').Any(part => part.Equals(keyName, StringComparison.InvariantCultureIgnoreCase))) // One path component
                        continue;
                }

                return true;
            }

            return false;
            
        }

        private static bool WildcardOrExact(string input, string pattern)
        {
            if (pattern.Contains('*') || pattern.Contains('?'))
                return WildcardMatch(input, pattern);
            return input.Equals(pattern, StringComparison.InvariantCultureIgnoreCase);
        }

        private static bool WildcardMatch(string input, string pattern)
        {
            int i = 0, p = 0, star = -1, mark = 0;
            while (i < input.Length)
            {
                if (p < pattern.Length && (pattern[p] == '?' || pattern[p] == input[i])) { i++; p++; continue; }
                if (p < pattern.Length && pattern[p] == '*') { star = p++; mark = i; continue; }
                if (star != -1) { p = star + 1; i = ++mark; continue; }
                return false;
            }
            while (p < pattern.Length && pattern[p] == '*') p++;
            return p == pattern.Length;
        }

        private void SearchButton_Click(object sender, EventArgs e)
        {
            string keyName = KeyTextbox.Text.ToLowerInvariant();
            string valName = ValueTextbox.Text.ToLowerInvariant();
            if (string.IsNullOrEmpty(keyName) & string.IsNullOrEmpty(valName))
            {
                MessageBox.Show("Please enter search terms.", "Policy Plus", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            if (new[] { @"HKLM\", @"HKCU\", @"HKEY_LOCAL_MACHINE\", @"HKEY_CURRENT_USER\" }.Any(bad => keyName.StartsWith(bad, StringComparison.InvariantCultureIgnoreCase)))
            {
                MessageBox.Show("Policies' root keys are determined only by their section. Remove the root key from the search terms and try again.", "Policy Plus", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
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