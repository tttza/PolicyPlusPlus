using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace PolicyPlus
{
    public partial class DetailPolicyFormatted
    {
        private IPolicySource CompPolSource, UserPolSource;
        public DetailPolicyFormatted()
        {
            InitializeComponent();
            this.CompPolSource = CompPolSource;
            this.UserPolSource = UserPolSource;
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
                      {"Administrative Templates", "管理用テンプレート"},
                      {"Key", "キー"},
                      {"Name", "名前"},
                      {"Type", "種類"},
                      {"Value", "値"}
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
                FormattedPolPathBox.Text += String.Concat(System.Environment.NewLine, new String(' ', 2 * depth_count), "+ ", name);
                depth_count++;
            }
            FormattedPolPathBox.Text += String.Concat(System.Environment.NewLine, new String(' ', 2 * depth_count), " ", Policy.DisplayName);

        }

        private void UpdateRegPathBox(PolicyPlusPolicy Policy, IPolicySource compPolSource, IPolicySource userPolSource, string languageCode)
        {
            var nl = System.Environment.NewLine;
            FormattedRegPathBox.Text = "";
            //FormattedRegPathBox.Text += $"{TranslateWords("Key", languageCode)}: {Policy.RawPolicy.RegistryKey}{nl}";
            //if (Policy.RawPolicy.RegistryValue != null)
            //{
            //    FormattedRegPathBox.Text += $"{TranslateWords("Name", languageCode)}: {Policy.RawPolicy.RegistryValue}{nl}";
            //}
            //if (Policy.RawPolicy.RegistryValue != null)
            //{
            //    FormattedRegPathBox.Text += $"{TranslateWords("Name", languageCode)}: {Policy.RawPolicy}{nl}";
            //}
            FormattedRegPathBox.Text += $"{nl}{GetRegistryString(compPolSource, PolicyState.Enabled, Policy, languageCode)}";
        }

        private string GetRegPathString(string regKey, string languageCode)
        {
            return $"{TranslateWords("Key", languageCode)}: {regKey}";
        }

        private string GetRegKeyString(string regVal, string languageCode)
        {
            return $"{TranslateWords("Name", languageCode)}: {regVal}";
        }
        private string GetRegValueString(string value, string languageCode)
        {
            return $"{TranslateWords("Value", languageCode)}: {value}";
        }

        private string GetRegTypeString(Microsoft.Win32.RegistryValueKind rvk, string languageCode)
        {
            var typeStr = $"{TranslateWords("Type", languageCode)}: ";
            switch (rvk)
            {
                case Microsoft.Win32.RegistryValueKind.DWord:
                    typeStr += "REG_DWORD";
                    break;

                case Microsoft.Win32.RegistryValueKind.QWord:
                    typeStr += "REG_QWORD";
                    break;

                case Microsoft.Win32.RegistryValueKind.String:
                    typeStr += "REG_SZ";
                    break;

                case Microsoft.Win32.RegistryValueKind.ExpandString:
                    typeStr += "REG_EXPAND_SZ";
                    break;

                case Microsoft.Win32.RegistryValueKind.Binary:
                    typeStr += "REG_BINARY";
                    break;

                case Microsoft.Win32.RegistryValueKind.MultiString:
                    typeStr += "REG_MULTI_SZ";
                    break;

                case Microsoft.Win32.RegistryValueKind.None:
                    typeStr += "None";
                    break;

                default:
                    typeStr += "Unknown";
                    break;
            }
            return typeStr;
        }

        public void PresentDialog(PolicyPlusPolicy Policy, IPolicySource compPolSource, IPolicySource userPolSource, string languageCode)
        {
            SelectedPolicy = Policy;
            NameTextbox.Text = Policy.DisplayName;
            IdTextbox.Text = Policy.UniqueID;
            DefinedTextbox.Text = Policy.RawPolicy.DefinedIn.SourceFile;

            if (languageCode == null) { languageCode = "en-US"; }

            UpdatePolPathBox(Policy, languageCode);
            UpdateRegPathBox(Policy, compPolSource, userPolSource, languageCode);
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
            if (tag == "PolPath")
            {
                Clipboard.SetText(FormattedPolPathBox.Text);
            }
            else if (tag == "RegPath")
            {
                Clipboard.SetText(FormattedRegPathBox.Text);
            }
        }

        private void DetailPolicyFormatted_Load(object sender, EventArgs e)
        {

        }

        String GetRegistryString(IPolicySource source, PolicyState policyState, PolicyPlusPolicy policy, string languageCode)
        {
            var regStr = "";
            var nl = System.Environment.NewLine;
            void setValue(string Key, string ValueName, PolicyRegistryValue Value)
            {
                if (Value is null)
                    return;
                switch (Value.RegistryType)
                {
                    case PolicyRegistryValueType.Delete:
                        {
                            //PolicySource.DeleteValue(Key, ValueName);
                            regStr += $"{GetRegPathString(Key, languageCode)}{nl}";
                            regStr += $"{GetRegKeyString(ValueName, languageCode)}{nl}";
                            regStr += $"Delete the value.{nl}";
                            regStr += $"{nl}";
                            break;
                        }

                    case PolicyRegistryValueType.Numeric:
                        {
                            //PolicySource.SetValue(Key, ValueName, Value.NumberValue, Microsoft.Win32.RegistryValueKind.DWord);
                            regStr += $"{GetRegPathString(Key, languageCode)}{nl}";
                            regStr += $"{GetRegKeyString(ValueName, languageCode)}{nl}";
                            regStr += $"{GetRegTypeString(Microsoft.Win32.RegistryValueKind.DWord, languageCode)}{nl}";
                            regStr += $"{GetRegValueString(Value.NumberValue.ToString(), languageCode)}{nl}";
                            regStr += $"{nl}";
                            break;
                        }

                    case PolicyRegistryValueType.Text:
                        {
                            //PolicySource.SetValue(Key, ValueName, Value.StringValue, Microsoft.Win32.RegistryValueKind.String);
                            regStr += $"{GetRegPathString(Key, languageCode)}{nl}";
                            regStr += $"{GetRegPathString(ValueName, languageCode)}{nl}";
                            regStr += $"{GetRegTypeString(Microsoft.Win32.RegistryValueKind.String, languageCode)}{nl}";
                            regStr += $"{GetRegValueString(Value.StringValue, languageCode)}{nl}";
                            regStr += $"{nl}";
                            break;
                        }
                }
            };
            void setSingleList(PolicyRegistrySingleList SingleList, string DefaultKey)
            {
                if (SingleList is null)
                    return;
                string listKey = string.IsNullOrEmpty(SingleList.DefaultRegistryKey) ? DefaultKey : SingleList.DefaultRegistryKey;
                foreach (var e in SingleList.AffectedValues)
                {
                    string itemKey = string.IsNullOrEmpty(e.RegistryKey) ? listKey : e.RegistryKey;
                    setValue(itemKey, e.RegistryValue, e.Value);
                }
            };
            void setList(PolicyRegistryList List, string DefaultKey, string DefaultValue, bool IsOn)
            {
                if (List is null)
                    return;
                if (IsOn)
                {
                    setValue(DefaultKey, DefaultValue, List.OnValue);
                    setSingleList(List.OnValueList, DefaultKey);
                }
                else
                {
                    setValue(DefaultKey, DefaultValue, List.OffValue);
                    setSingleList(List.OffValueList, DefaultKey);
                }
            };
            var rawpol = policy.RawPolicy;
            var options = PolicyProcessing.GetPolicyOptionStates(source, policy);

            var state = PolicyProcessing.GetPolicyState(source, policy);
            switch (state)
            {
                case PolicyState.Enabled:
                    {
                        if (rawpol.AffectedValues.OnValue is null & !string.IsNullOrEmpty(rawpol.RegistryValue))
                        {
                            //PolicySource.SetValue(rawpol.RegistryKey, rawpol.RegistryValue, 1U, Microsoft.Win32.RegistryValueKind.DWord);
                            regStr += $"{GetRegPathString(rawpol.RegistryKey, languageCode)}{nl}";
                            regStr += $"{GetRegKeyString(rawpol.RegistryValue, languageCode)}{nl}";
                            regStr += $"{GetRegTypeString(Microsoft.Win32.RegistryValueKind.DWord, languageCode)}{nl}";
                            regStr += $"{GetRegValueString(1U.ToString(), languageCode)}{nl}";
                            regStr += $"{nl}";
                        }

                        setList(rawpol.AffectedValues, rawpol.RegistryKey, rawpol.RegistryValue, true);
                        if (rawpol.Elements is object) // Write the elements' states
                        {
                            foreach (var elem in rawpol.Elements)
                            {
                                string elemKey = string.IsNullOrEmpty(elem.RegistryKey) ? rawpol.RegistryKey : elem.RegistryKey;
                                if (!options.ContainsKey(elem.ID))
                                    continue;
                                var optionData = options[elem.ID];
                                switch (elem.ElementType ?? "")
                                {
                                    case "decimal":
                                        {
                                            DecimalPolicyElement decimalElem = (DecimalPolicyElement)elem;
                                            if (decimalElem.StoreAsText)
                                            {
                                                //PolicySource.SetValue(elemKey, elem.RegistryValue, Conversions.ToString(optionData), Microsoft.Win32.RegistryValueKind.String);
                                                regStr += $"{GetRegPathString(elemKey, languageCode)}{nl}";
                                                regStr += $"{GetRegKeyString(elem.RegistryValue, languageCode)}{nl}";
                                                regStr += $"{GetRegTypeString(Microsoft.Win32.RegistryValueKind.String, languageCode)}{nl}";
                                                regStr += $"{GetRegValueString(optionData.ToString(), languageCode)}{nl}";
                                                regStr += $"{nl}";
                                            }
                                            else
                                            {
                                                //PolicySource.SetValue(elemKey, elem.RegistryValue, Conversions.ToUInteger(optionData), Microsoft.Win32.RegistryValueKind.DWord);
                                                regStr += $"{GetRegPathString(elemKey, languageCode)}{nl}";
                                                regStr += $"{GetRegKeyString(elem.RegistryValue, languageCode)}{nl}";
                                                regStr += $"{GetRegTypeString(Microsoft.Win32.RegistryValueKind.DWord, languageCode)}{nl}";
                                                regStr += $"{GetRegValueString(optionData.ToString(), languageCode)}{nl}";
                                                regStr += $"{nl}";
                                            }

                                            break;
                                        }

                                    case "boolean":
                                        {
                                            BooleanPolicyElement booleanElem = (BooleanPolicyElement)elem;
                                            bool checkState = (bool)(optionData);
                                            if (booleanElem.AffectedRegistry.OnValue is null & checkState)
                                            {
                                                //PolicySource.SetValue(elemKey, elem.RegistryValue, 1U, Microsoft.Win32.RegistryValueKind.DWord);
                                                regStr += $"{GetRegPathString(elemKey, languageCode)}{nl}";
                                                regStr += $"{GetRegKeyString(elem.RegistryValue, languageCode)}{nl}";
                                                regStr += $"{GetRegTypeString(Microsoft.Win32.RegistryValueKind.DWord, languageCode)}{nl}";
                                                regStr += $"{GetRegValueString(1U.ToString(), languageCode)}{nl}";
                                                regStr += $"{nl}";
                                            }

                                            if (booleanElem.AffectedRegistry.OffValue is null & !checkState)
                                            {
                                                //PolicySource.DeleteValue(elemKey, elem.RegistryValue);
                                                regStr += $"{GetRegPathString(elemKey, languageCode)}{nl}";
                                                regStr += $"{GetRegKeyString(elem.RegistryValue, languageCode)}{nl}";
                                                regStr += $"{GetRegTypeString(Microsoft.Win32.RegistryValueKind.None, languageCode)}{nl}";
                                                regStr += $"{GetRegValueString("UNSET", languageCode)}{nl}";
                                                regStr += $"{nl}";
                                            }

                                            setList(booleanElem.AffectedRegistry, elemKey, elem.RegistryValue, checkState);
                                            break;
                                        }

                                    case "text":
                                        {
                                            TextPolicyElement textElem = (TextPolicyElement)elem;
                                            var regType = textElem.RegExpandSz ? Microsoft.Win32.RegistryValueKind.ExpandString : Microsoft.Win32.RegistryValueKind.String;
                                            //PolicySource.SetValue(elemKey, elem.RegistryValue, optionData, regType);
                                            regStr += $"{GetRegPathString(elemKey, languageCode)}{nl}";
                                            regStr += $"{GetRegKeyString(elem.RegistryValue, languageCode)}{nl}";
                                            regStr += $"{GetRegTypeString(regType, languageCode)}{nl}";
                                            regStr += $"{GetRegValueString(optionData.ToString(), languageCode)}{nl}";
                                            regStr += $"{nl}";
                                            break;
                                        }

                                    case "list":
                                        {
                                            ListPolicyElement listElem = (ListPolicyElement)elem;
                                            if (!listElem.NoPurgeOthers)
                                                //PolicySource.ClearKey(elemKey);
                                                if (optionData is null)
                                                    continue;
                                            var regType = listElem.RegExpandSz ? Microsoft.Win32.RegistryValueKind.ExpandString : Microsoft.Win32.RegistryValueKind.String;
                                            if (listElem.UserProvidesNames)
                                            {
                                                regStr += $"{GetRegPathString(elemKey, languageCode)}{nl}";
                                                Dictionary<string, string> items = (Dictionary<string, string>)optionData;
                                                foreach (var i in items)
                                                {
                                                    Console.Write(i);
                                                    //PolicySource.SetValue(elemKey, i.Key, i.Value, regType);
                                                    regStr += $"{GetRegKeyString(i.Key, languageCode)}{nl}";
                                                    regStr += $"{GetRegTypeString(regType, languageCode)}{nl}";
                                                    regStr += $"{GetRegValueString(i.Value.ToString(), languageCode)}{nl}";
                                                    regStr += $"{nl}";
                                                }
                                            }
                                            else
                                            {
                                                List<string> items = (List<string>)optionData;
                                                int n = 1;
                                                while (n <= items.Count)
                                                {
                                                    string valueName = listElem.HasPrefix ? listElem.RegistryValue + n : items[n - 1];
                                                    //PolicySource.SetValue(elemKey, valueName, items[n - 1], regType);
                                                    regStr += $"{GetRegKeyString(valueName, languageCode)}{nl}";
                                                    regStr += $"{GetRegTypeString(regType, languageCode)}{nl}";
                                                    regStr += $"{GetRegValueString(items[n - 1].ToString(), languageCode)}{nl}";
                                                    regStr += $"{nl}";
                                                    n += 1;
                                                }
                                            }

                                            break;
                                        }

                                    case "enum":
                                        {
                                            EnumPolicyElement enumElem = (EnumPolicyElement)elem;
                                            var selItem = enumElem.Items[(int)(optionData)];
                                            setValue(elemKey, elem.RegistryValue, selItem.Value);
                                            setSingleList(selItem.ValueList, elemKey);
                                            break;
                                        }

                                    case "multiText":
                                        {
                                            //PolicySource.SetValue(elemKey, elem.RegistryValue, optionData, Microsoft.Win32.RegistryValueKind.MultiString);
                                            regStr += $"{GetRegPathString(elemKey, languageCode)}{nl}";
                                            regStr += $"{GetRegKeyString(elem.RegistryValue, languageCode)}{nl}";
                                            regStr += $"{GetRegTypeString(Microsoft.Win32.RegistryValueKind.MultiString, languageCode)}{nl}";
                                            regStr += $"{GetRegValueString(optionData.ToString(), languageCode)}{nl}";
                                            regStr += $"{nl}";
                                            break;
                                        }
                                }
                            }
                        }

                        break;
                    }

                case PolicyState.Disabled:
                    {
                        if (rawpol.AffectedValues.OffValue is null & !string.IsNullOrEmpty(rawpol.RegistryValue))
                        {
                            //PolicySource.DeleteValue(rawpol.RegistryKey, rawpol.RegistryValue);
                            regStr += $"{GetRegPathString(rawpol.RegistryKey, languageCode)}{nl}";
                            regStr += $"{GetRegKeyString(rawpol.RegistryValue, languageCode)}{nl}";
                            regStr += $"Delete the value.{nl}";
                            regStr += $"{nl}";
                        }

                        setList(rawpol.AffectedValues, rawpol.RegistryKey, rawpol.RegistryValue, false);
                        if (rawpol.Elements is object) // Mark all the elements deleted
                        {
                            foreach (var elem in rawpol.Elements)
                            {
                                string elemKey = string.IsNullOrEmpty(elem.RegistryKey) ? rawpol.RegistryKey : elem.RegistryKey;
                                if (elem.ElementType == "list")
                                {
                                    //PolicySource.ClearKey(elemKey);
                                    regStr += $"{GetRegPathString(elemKey, languageCode)}{nl}";
                                    regStr += $"Delete the key.{nl}";
                                    regStr += $"{nl}";
                                }
                                else if (elem.ElementType == "boolean")
                                {
                                    BooleanPolicyElement booleanElem = (BooleanPolicyElement)elem;
                                    if (booleanElem.AffectedRegistry.OffValue is object | booleanElem.AffectedRegistry.OffValueList is object)
                                    {
                                        // Non-implicit checkboxes get their "off" value set when the policy is disabled
                                        setList(booleanElem.AffectedRegistry, elemKey, elem.RegistryValue, false);
                                    }
                                    else
                                    {
                                        //PolicySource.DeleteValue(elemKey, elem.RegistryValue);
                                        regStr += $"{GetRegPathString(rawpol.RegistryKey, languageCode)}{nl}";
                                        regStr += $"{GetRegKeyString(rawpol.RegistryValue, languageCode)}{nl}";
                                        regStr += $"Delete the value.{nl}";
                                        regStr += $"{nl}";
                                    }
                                }
                                else
                                {
                                    //PolicySource.DeleteValue(elemKey, elem.RegistryValue);
                                    regStr += $"{GetRegPathString(rawpol.RegistryKey, languageCode)}{nl}";
                                    regStr += $"{GetRegKeyString(rawpol.RegistryValue, languageCode)}{nl}";
                                    regStr += $"Delete the value.{nl}";
                                    regStr += $"{nl}";
                                }
                            }
                        }

                        break;
                    }
                case PolicyState.NotConfigured:
                    {
                        if (rawpol.AffectedValues.OffValue is null & !string.IsNullOrEmpty(rawpol.RegistryValue))
                        {
                            //PolicySource.DeleteValue(rawpol.RegistryKey, rawpol.RegistryValue);
                            regStr += $"{GetRegPathString(rawpol.RegistryKey, languageCode)}{nl}";
                            regStr += $"{GetRegKeyString(rawpol.RegistryValue, languageCode)}{nl}";
                            regStr += $"{GetRegTypeString(Microsoft.Win32.RegistryValueKind.DWord, languageCode)}{nl}";
                            regStr += $"Delete the value.{nl}";
                            regStr += $"{nl}";
                        }
                        //setList(rawpol.AffectedValues, rawpol.RegistryKey, rawpol.RegistryValue, false);
                        if (rawpol.AffectedValues is not null)
                        {
                            var Value = rawpol.AffectedValues.OffValue;
                            var Key = rawpol.RegistryKey;
                            var ValueName = rawpol.RegistryValue;
                            if (Value is not null)
                            {
                                switch (Value.RegistryType)
                                {
                                    case PolicyRegistryValueType.Delete:
                                        {
                                            //PolicySource.DeleteValue(Key, ValueName);
                                            regStr += $"{GetRegPathString(Key, languageCode)}{nl}";
                                            regStr += $"{GetRegKeyString(ValueName, languageCode)}{nl}";
                                            regStr += $"Delete the value.{nl}";
                                            regStr += $"{nl}";
                                            break;
                                        }

                                    case PolicyRegistryValueType.Numeric:
                                        {
                                            //PolicySource.SetValue(Key, ValueName, Value.NumberValue, Microsoft.Win32.RegistryValueKind.DWord);
                                            regStr += $"{GetRegPathString(Key, languageCode)}{nl}";
                                            regStr += $"{GetRegKeyString(ValueName, languageCode)}{nl}";
                                            regStr += $"{GetRegTypeString(Microsoft.Win32.RegistryValueKind.DWord, languageCode)}{nl}";
                                            regStr += $"Delete the value.{nl}";
                                            regStr += $"{nl}";
                                            break;
                                        }

                                    case PolicyRegistryValueType.Text:
                                        {
                                            //PolicySource.SetValue(Key, ValueName, Value.StringValue, Microsoft.Win32.RegistryValueKind.String);
                                            regStr += $"{GetRegPathString(Key, languageCode)}{nl}";
                                            regStr += $"{GetRegPathString(ValueName, languageCode)}{nl}";
                                            regStr += $"{GetRegTypeString(Microsoft.Win32.RegistryValueKind.String, languageCode)}{nl}";
                                            regStr += $"{GetRegValueString(Value.StringValue, languageCode)}{nl}";
                                            regStr += $"Delete the value.{nl}";
                                            regStr += $"{nl}";
                                            break;
                                        }
                                }
                            }

                        }

                        if (rawpol.AffectedValues.OnValue is null & !string.IsNullOrEmpty(rawpol.RegistryValue))
                        {
                            //PolicySource.SetValue(rawpol.RegistryKey, rawpol.RegistryValue, 1U, Microsoft.Win32.RegistryValueKind.DWord);
                            regStr += $"{GetRegPathString(rawpol.RegistryKey, languageCode)}{nl}";
                            regStr += $"{GetRegKeyString(rawpol.RegistryValue, languageCode)}{nl}";
                            regStr += $"{GetRegTypeString(Microsoft.Win32.RegistryValueKind.DWord, languageCode)}{nl}";
                            regStr += $"Delete the value.{nl}";
                            regStr += $"{nl}";
                        }

                        if (rawpol.Elements is object) // Mark all the elements deleted
                        {
                            foreach (var elem in rawpol.Elements)
                            {
                                string elemKey = string.IsNullOrEmpty(elem.RegistryKey) ? rawpol.RegistryKey : elem.RegistryKey;
                                if (elem.ElementType == "list")
                                {
                                    //PolicySource.ClearKey(elemKey);
                                    regStr += $"{GetRegPathString(elemKey, languageCode)}{nl}";
                                    regStr += $"Delete the key.{nl}";
                                    regStr += $"{nl}";
                                }
                                else if (elem.ElementType == "boolean")
                                {
                                    BooleanPolicyElement booleanElem = (BooleanPolicyElement)elem;
                                    if (booleanElem.AffectedRegistry.OffValue is object | booleanElem.AffectedRegistry.OffValueList is object)
                                    {
                                        // Non-implicit checkboxes get their "off" value set when the policy is disabled
                                        setList(booleanElem.AffectedRegistry, elemKey, elem.RegistryValue, false);
                                    }
                                    else
                                    {
                                        //PolicySource.DeleteValue(elemKey, elem.RegistryValue);
                                        regStr += $"{GetRegPathString(rawpol.RegistryKey, languageCode)}{nl}";
                                        regStr += $"{GetRegKeyString(rawpol.RegistryValue, languageCode)}{nl}";
                                        regStr += $"Delete the value.{nl}";
                                        regStr += $"{nl}";
                                    }
                                }
                                //else if (elem.ElementType == "enum")
                                //{
                                //    if (!options.ContainsKey(elem.ID))
                                //        continue;
                                //    var optionData = options[elem.ID];
                                //    EnumPolicyElement enumElem = (EnumPolicyElement)elem;
                                //    var selItem = enumElem.Items[(int)(optionData)];
                                //    setValue(elemKey, elem.RegistryValue, selItem.Value);
                                //    setSingleList(selItem.ValueList, elemKey);
                                //    break;
                                //}
                                else
                                {
                                    //PolicySource.DeleteValue(elemKey, elem.RegistryValue);
                                    regStr += $"{GetRegPathString(elemKey, languageCode)}{nl}";
                                    regStr += $"{GetRegKeyString(elem.RegistryValue, languageCode)}{nl}";
                                    regStr += $"Delete the value.{nl}";
                                    regStr += $"{nl}";
                                }
                            }
                        }
                        break;
                    }
            }
            return regStr;


        }
    }
}
