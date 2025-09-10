using Microsoft.Win32;

using PolicyPlusCore.IO;

using System;
using System.Linq;

namespace PolicyPlus.UI.PolicyDetail;

public partial class EditPol
{
    public EditPol()
    {
        InitializeComponent();
    }

    public PolFile EditingPol;
    private bool EditingUserSource;

    public void UpdateTree()
    {
        var topItemIndex = default(int?);
        if (LsvPol.TopItem is object)
            topItemIndex = LsvPol.TopItem.Index;
        LsvPol.BeginUpdate();
        LsvPol.Items.Clear();
        void addKey(string Prefix, int Depth)
        {
            var subkeys = EditingPol.GetKeyNames(Prefix);
            subkeys.Sort(StringComparer.InvariantCultureIgnoreCase);
            foreach (var subkey in subkeys)
            {
                string keypath = string.IsNullOrEmpty(Prefix) ? subkey : Prefix + @"\" + subkey;
                var lsvi = LsvPol.Items.Add(subkey);
                lsvi.IndentCount = Depth;
                lsvi.ImageIndex = 0;
                lsvi.Tag = keypath;
                addKey(keypath, Depth + 1);
            }

            var values = EditingPol.GetValueNames(Prefix, false);
            values.Sort(StringComparer.InvariantCultureIgnoreCase);
            foreach (var value in values)
            {
                if (string.IsNullOrEmpty(value))
                    continue;
                var data = EditingPol.GetValue(Prefix, value);
                var kind = EditingPol.GetValueKind(Prefix, value);
                System.Windows.Forms.ListViewItem addToLsv(string ItemText, int Icon, bool Deletion)
                {
                    var lsvItem = LsvPol.Items.Add(ItemText, Icon);
                    lsvItem.IndentCount = Depth;
                    var tag = new PolValueInfo { Name = value, Key = Prefix };
                    if (Deletion)
                    {
                        tag.IsDeleter = true;
                    }
                    else
                    {
                        tag.Kind = kind;
                        tag.Data = data;
                    }
                    lsvItem.Tag = tag;
                    return lsvItem;
                }
                if (value.Equals("**deletevalues", StringComparison.InvariantCultureIgnoreCase))
                {
                    addToLsv("Delete values", 8, true).SubItems.Add(data.ToString());
                }
                else if (value.StartsWith("**del.", StringComparison.InvariantCultureIgnoreCase))
                {
                    addToLsv("Delete value", 8, true).SubItems.Add(value.Substring(6));
                }
                else if (value.StartsWith("**delvals", StringComparison.InvariantCultureIgnoreCase))
                {
                    addToLsv("Delete all values", 8, true);
                }
                else
                {
                    string text = string.Empty;
                    int iconIndex = 0;
                    switch (data)
                    {
                        case string[] sa:
                            text = string.Join(" ", sa);
                            iconIndex = 39;
                            break;
                        case string s:
                            text = s;
                            iconIndex = kind == RegistryValueKind.ExpandString ? 42 : 40;
                            break;
                        case uint:
                            text = data.ToString();
                            iconIndex = 15;
                            break;
                        case ulong:
                            text = data.ToString();
                            iconIndex = 41;
                            break;
                        case byte[] bytes:
                            text = BitConverter.ToString(bytes).Replace("-", " ");
                            iconIndex = 13;
                            break;
                        default:
                            text = data?.ToString() ?? string.Empty;
                            break;
                    }
                    addToLsv(value, iconIndex, false).SubItems.Add(text);
                }
            }
        }
        addKey(string.Empty, 0);
        LsvPol.EndUpdate();
        if (topItemIndex.HasValue && LsvPol.Items.Count > topItemIndex.Value)
            LsvPol.TopItem = LsvPol.Items[topItemIndex.Value];
    }

    public void PresentDialog(System.Windows.Forms.ImageList Images, PolFile Pol, bool IsUser)
    {
        LsvPol.SmallImageList = Images;
        EditingPol = Pol;
        EditingUserSource = IsUser;
        UpdateTree();
        ChItem.Width = LsvPol.ClientSize.Width - ChValue.Width - System.Windows.Forms.SystemInformation.VerticalScrollBarWidth;
        LsvPol_SelectedIndexChanged(null, null);
        ShowDialog();
    }

    private void ButtonSave_Click(object sender, EventArgs e) => DialogResult = System.Windows.Forms.DialogResult.OK;

    private void EditPol_KeyDown(object sender, System.Windows.Forms.KeyEventArgs e)
    {
        if (e.KeyCode == System.Windows.Forms.Keys.Escape)
            DialogResult = System.Windows.Forms.DialogResult.Cancel;
    }

    public void SelectKey(string KeyPath)
    {
        var lsvi = LsvPol.Items.OfType<System.Windows.Forms.ListViewItem>()
            .FirstOrDefault(i => i.Tag is string s && KeyPath.Equals(s, StringComparison.InvariantCultureIgnoreCase));
        if (lsvi is null) return;
        lsvi.Selected = true;
        lsvi.EnsureVisible();
    }

    public void SelectValue(string KeyPath, string ValueName)
    {
        var lsvi = LsvPol.Items.OfType<System.Windows.Forms.ListViewItem>()
            .FirstOrDefault(item => item.Tag is PolValueInfo pvi &&
                                     pvi.Key.Equals(KeyPath, StringComparison.InvariantCultureIgnoreCase) &&
                                     pvi.Name.Equals(ValueName, StringComparison.InvariantCultureIgnoreCase));
        if (lsvi is null) return;
        lsvi.Selected = true;
        lsvi.EnsureVisible();
    }

    public bool IsKeyNameValid(string Name) => !Name.Contains('\\');
    public bool IsKeyNameAvailable(string ContainerPath, string Name) => !EditingPol.GetKeyNames(ContainerPath).Any(k => k.Equals(Name, StringComparison.InvariantCultureIgnoreCase));

    private void ButtonAddKey_Click(object sender, EventArgs e)
    {
    string keyName = AppForms.EditPolKey.PresentDialog(string.Empty);
        if (string.IsNullOrEmpty(keyName)) return;
        if (!IsKeyNameValid(keyName))
        {
            System.Windows.Forms.MessageBox.Show("The key name is not valid.", "Policy Plus", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Exclamation);
            return;
        }
        string containerKey = LsvPol.SelectedItems.Count > 0 ? LsvPol.SelectedItems[0].Tag as string ?? string.Empty : string.Empty;
        if (!IsKeyNameAvailable(containerKey, keyName))
        {
            System.Windows.Forms.MessageBox.Show("The key name is already taken.", "Policy Plus", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Exclamation);
            return;
        }
        string newPath = string.IsNullOrEmpty(containerKey) ? keyName : containerKey + @"\" + keyName;
        EditingPol.SetValue(newPath, string.Empty, Array.Empty<byte>(), RegistryValueKind.None);
        UpdateTree();
        SelectKey(newPath);
    }

    public object PromptForNewValueData(string ValueName, object CurrentData, RegistryValueKind Kind)
    {
        if (Kind == RegistryValueKind.String || Kind == RegistryValueKind.ExpandString)
        {
            if (AppForms.EditPolStringData.PresentDialog(ValueName, CurrentData as string ?? string.Empty) == System.Windows.Forms.DialogResult.OK)
                return AppForms.EditPolStringData.TextData.Text;
            return null;
        }
        if (Kind == RegistryValueKind.DWord || Kind == RegistryValueKind.QWord)
        {
            if (AppForms.EditPolNumericData.PresentDialog(ValueName, Convert.ToUInt64(CurrentData), Kind == RegistryValueKind.QWord) == System.Windows.Forms.DialogResult.OK)
                return AppForms.EditPolNumericData.NumData.Value;
            return null;
        }
        if (Kind == RegistryValueKind.MultiString)
        {
            if (AppForms.EditPolMultiStringData.PresentDialog(ValueName, (string[])CurrentData) == System.Windows.Forms.DialogResult.OK)
                return AppForms.EditPolMultiStringData.TextData.Lines;
            return null;
        }
        System.Windows.Forms.MessageBox.Show("This value kind is not supported.", "Policy Plus", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Exclamation);
        return null;
    }

    private void ButtonAddValue_Click(object sender, EventArgs e)
    {
        string keyPath = LsvPol.SelectedItems[0].Tag as string ?? string.Empty;
    if (AppForms.EditPolValue.PresentDialog() != System.Windows.Forms.DialogResult.OK) return;
    string value = AppForms.EditPolValue.ChosenName;
    var kind = AppForms.EditPolValue.SelectedKind;
        object defaultData = kind switch
        {
            RegistryValueKind.String or RegistryValueKind.ExpandString => string.Empty,
            RegistryValueKind.DWord or RegistryValueKind.QWord => 0,
            _ => Array.Empty<string>()
        };
        var newData = PromptForNewValueData(value, defaultData, kind);
        if (newData is not null)
        {
            EditingPol.SetValue(keyPath, value, newData, kind);
            UpdateTree();
            SelectValue(keyPath, value);
        }
    }

    private void ButtonDeleteValue_Click(object sender, EventArgs e)
    {
        var tag = LsvPol.SelectedItems[0].Tag;
        if (tag is string keyTag)
        {
            if (AppForms.EditPolDelete.PresentDialog(keyTag.Split('\\').Last()) != System.Windows.Forms.DialogResult.OK)
                return;
            if (AppForms.EditPolDelete.OptPurge.Checked)
            {
                EditingPol.ClearKey(keyTag);
            }
            else if (AppForms.EditPolDelete.OptClearFirst.Checked)
            {
                EditingPol.ForgetKeyClearance(keyTag);
                EditingPol.ClearKey(keyTag);
                int index = LsvPol.SelectedIndices[0] + 1;
                while (index < LsvPol.Items.Count)
                {
                    var subItem = LsvPol.Items[index];
                    if (subItem.IndentCount <= LsvPol.SelectedItems[0].IndentCount)
                        break;
                    if (subItem.IndentCount == LsvPol.SelectedItems[0].IndentCount + 1 && subItem.Tag is PolValueInfo valueInfo && !valueInfo.IsDeleter)
                        EditingPol.SetValue(valueInfo.Key, valueInfo.Name, valueInfo.Data, valueInfo.Kind);
                    index++;
                }
            }
            else
            {
                EditingPol.DeleteValue(keyTag, AppForms.EditPolDelete.TextValueName.Text);
            }
            UpdateTree();
            SelectKey(keyTag);
        }
        else
        {
            PolValueInfo info = (PolValueInfo)tag;
            EditingPol.DeleteValue(info.Key, info.Name);
            UpdateTree();
            SelectValue(info.Key, "**del." + info.Name);
        }
    }

    private void ButtonForget_Click(object sender, EventArgs e)
    {
        string containerKey = string.Empty;
        var tag = LsvPol.SelectedItems[0].Tag;
        if (tag is string keyTag)
        {
            if (System.Windows.Forms.MessageBox.Show("Are you sure you want to remove this key and all its contents?", "Policy Plus", System.Windows.Forms.MessageBoxButtons.YesNo, System.Windows.Forms.MessageBoxIcon.Exclamation) == System.Windows.Forms.DialogResult.No)
                return;
            string keyPath = keyTag;
            if (keyPath.Contains('\\'))
                containerKey = keyPath.Remove(keyPath.LastIndexOf('\\'));
            void removeKey(string Key)
            {
                foreach (var subkey in EditingPol.GetKeyNames(Key))
                    removeKey(Key + @"\" + subkey);
                EditingPol.ClearKey(Key);
                EditingPol.ForgetKeyClearance(Key);
            }
            removeKey(keyPath);
        }
        else
        {
            PolValueInfo info = (PolValueInfo)tag;
            containerKey = info.Key;
            EditingPol.ForgetValue(info.Key, info.Name);
        }
        UpdateTree();
        if (!string.IsNullOrEmpty(containerKey))
        {
            var parts = containerKey.Split('\\');
            for (int n = 1; n <= parts.Length; n++)
                SelectKey(string.Join(@"\", parts.Take(n)));
        }
        else
        {
            LsvPol_SelectedIndexChanged(null, null);
        }
    }

    private void ButtonEdit_Click(object sender, EventArgs e)
    {
        PolValueInfo info = (PolValueInfo)LsvPol.SelectedItems[0].Tag;
        var newData = PromptForNewValueData(info.Name, info.Data, info.Kind);
        if (newData is not null)
        {
            EditingPol.SetValue(info.Key, info.Name, newData, info.Kind);
            UpdateTree();
            SelectValue(info.Key, info.Name);
        }
    }

    private void LsvPol_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (LsvPol.SelectedItems.Count == 0)
        {
            ButtonAddKey.Enabled = true;
            ButtonAddValue.Enabled = false;
            ButtonDeleteValue.Enabled = false;
            ButtonEdit.Enabled = false;
            ButtonForget.Enabled = false;
            ButtonExport.Enabled = true;
        }
        else
        {
            var tag = LsvPol.SelectedItems[0].Tag;
            ButtonForget.Enabled = true;
            if (tag is string)
            {
                ButtonAddKey.Enabled = true;
                ButtonAddValue.Enabled = true;
                ButtonEdit.Enabled = false;
                ButtonDeleteValue.Enabled = true;
                ButtonExport.Enabled = true;
            }
            else
            {
                ButtonAddKey.Enabled = false;
                ButtonAddValue.Enabled = false;
                bool delete = ((PolValueInfo)tag).IsDeleter;
                ButtonEdit.Enabled = !delete;
                ButtonDeleteValue.Enabled = !delete;
                ButtonExport.Enabled = false;
            }
        }
    }

    private void ButtonImport_Click(object sender, EventArgs e)
    {
    if (AppForms.ImportReg.PresentDialog(EditingPol) == System.Windows.Forms.DialogResult.OK)
            UpdateTree();
    }

    private void ButtonExport_Click(object sender, EventArgs e)
    {
        string branch = LsvPol.SelectedItems.Count > 0 ? LsvPol.SelectedItems[0].Tag as string ?? string.Empty : string.Empty;
    AppForms.ExportReg.PresentDialog(branch, EditingPol, EditingUserSource);
    }

    private class PolValueInfo
    {
        public string Key;
        public string Name;
        public RegistryValueKind Kind;
        public object Data;
        public bool IsDeleter;
    }
}