using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using PolicyPlus.Core.IO;
using PolicyPlus.Core.Admx;

namespace PolicyPlus.Core.Core
{
    public class PolicyProcessing
    {
        public static PolicyState GetPolicyState(IPolicySource PolicySource, PolicyPlusPolicy Policy)
        {
            decimal enabledEvidence = 0m;
            decimal disabledEvidence = 0m;
            bool hasRegistryValue = false;
            var rawpol = Policy.RawPolicy;
            if (rawpol.AffectedValues == null)
                rawpol.AffectedValues = new PolicyRegistryList();
            void checkOneVal(PolicyRegistryValue? Value, string Key, string ValueName, ref decimal EvidenceVar)
            {
                if (Value is null)
                    return;
                if (ValuePresent(Value, PolicySource, Key, ValueName))
                    EvidenceVar += 1m;
            };
            void checkValList(PolicyRegistrySingleList? ValList, string DefaultKey, ref decimal EvidenceVar)
            {
                if (ValList is null)
                    return;
                string listKey = string.IsNullOrEmpty(ValList.DefaultRegistryKey) ? DefaultKey : ValList.DefaultRegistryKey;
                foreach (var regVal in ValList.AffectedValues)
                {
                    string entryKey = string.IsNullOrEmpty(regVal.RegistryKey) ? listKey : regVal.RegistryKey;
                    checkOneVal(regVal.Value, entryKey, regVal.RegistryValue, ref EvidenceVar);
                }
            };
            if (!string.IsNullOrEmpty(rawpol.RegistryValue))
            {
                if (rawpol.AffectedValues.OnValue is null)
                {
                    hasRegistryValue = true;
                    checkOneVal(new PolicyRegistryValue() { NumberValue = 1U, RegistryType = PolicyRegistryValueType.Numeric }, rawpol.RegistryKey, rawpol.RegistryValue, ref enabledEvidence);
                }
                else
                {
                    checkOneVal(rawpol.AffectedValues.OnValue, rawpol.RegistryKey, rawpol.RegistryValue, ref enabledEvidence);
                }

                if (rawpol.AffectedValues.OffValue is null)
                {
                    hasRegistryValue = true;
                    checkOneVal(new PolicyRegistryValue() { RegistryType = PolicyRegistryValueType.Delete }, rawpol.RegistryKey, rawpol.RegistryValue, ref disabledEvidence);
                }
                else
                {
                    checkOneVal(rawpol.AffectedValues.OffValue, rawpol.RegistryKey, rawpol.RegistryValue, ref disabledEvidence);
                }
            }

            checkValList(rawpol.AffectedValues.OnValueList, rawpol.RegistryKey, ref enabledEvidence);
            checkValList(rawpol.AffectedValues.OffValueList, rawpol.RegistryKey, ref disabledEvidence);
            if (rawpol.Elements is object)
            {
                decimal deletedElements = 0m;
                decimal presentElements = 0m;
                foreach (var elem in rawpol.Elements)
                {
                    string elemKey = string.IsNullOrEmpty(elem.RegistryKey) ? rawpol.RegistryKey : elem.RegistryKey;
                    if (elem.ElementType == "list")
                    {
                        int neededValues = 0;
                        if (PolicySource.WillDeleteValue(elemKey, ""))
                        {
                            deletedElements += 1m;
                            neededValues = 1;
                        }

                        if (PolicySource.GetValueNames(elemKey).Count > 0)
                        {
                            deletedElements -= neededValues;
                            presentElements += 1m;
                        }
                    }
                    else if (elem.ElementType == "boolean")
                    {
                        BooleanPolicyElement booleanElem = (BooleanPolicyElement)elem;
                        if (PolicySource.WillDeleteValue(elemKey, elem.RegistryValue))
                        {
                            deletedElements += 1m;
                        }
                        else
                        {
                            decimal checkboxDisabled = 0m;
                            checkOneVal(booleanElem.AffectedRegistry.OffValue, elemKey, elem.RegistryValue, ref checkboxDisabled);
                            checkValList(booleanElem.AffectedRegistry.OffValueList, elemKey, ref checkboxDisabled);
                            deletedElements += checkboxDisabled * 0.1m;
                            checkOneVal(booleanElem.AffectedRegistry.OnValue, elemKey, elem.RegistryValue, ref presentElements);
                            checkValList(booleanElem.AffectedRegistry.OnValueList, elemKey, ref presentElements);
                        }
                    }
                    else if (PolicySource.WillDeleteValue(elemKey, elem.RegistryValue))
                    {
                        if (!hasRegistryValue)
                        {
                            deletedElements += 1m;
                        }
                    }
                    else if (PolicySource.ContainsValue(elemKey, elem.RegistryValue))
                    {
                        if (!hasRegistryValue)
                        {
                            presentElements += 1m;
                        }
                    }
                }

                if (presentElements > 0m)
                {
                    enabledEvidence += presentElements;
                }
                else if (deletedElements > 0m)
                {
                    disabledEvidence += deletedElements;
                }
            }
            if (enabledEvidence > disabledEvidence)
            {
                return PolicyState.Enabled;
            }
            else if (disabledEvidence > enabledEvidence)
            {
                return PolicyState.Disabled;
            }
            else if (enabledEvidence == 0m)
            {
                return PolicyState.NotConfigured;
            }
            else
            {
                return PolicyState.Unknown;
            }
        }

        private static uint TryGetUInt32(object value)
        {
            if (value == null)
                return 0U;
            switch (value)
            {
                case uint u:
                    return u;
                case int i when i >= 0:
                    return (uint)i;
                case long l when l >= 0 && l <= uint.MaxValue:
                    return (uint)l;
                case ulong ul when ul <= uint.MaxValue:
                    return (uint)ul;
                case string s:
                    {
                        s = s.Trim();
                        if (s.Length == 0)
                            return 0U;
                        uint parsed;
                        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase) || s.StartsWith("&H", StringComparison.OrdinalIgnoreCase))
                        {
                            var hex = s.Substring(2);
                            if (uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out parsed))
                                return parsed;
                        }
                        if (uint.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
                            return parsed;
                        return 0U;
                    }
                default:
                    try
                    {
                        return Convert.ToUInt32(value, CultureInfo.InvariantCulture);
                    }
                    catch
                    {
                        return 0U;
                    }
            }
        }

    private static bool ValuePresent(PolicyRegistryValue? Value, IPolicySource Source, string Key, string ValueName)
        {
            if (Value == null)
                return false;
            switch (Value.RegistryType)
            {
                case PolicyRegistryValueType.Delete:
                    {
                        return Source.WillDeleteValue(Key, ValueName);
                    }

                case PolicyRegistryValueType.Numeric:
                    {
                        if (!Source.ContainsValue(Key, ValueName))
                            return false;
                        var sourceVal = Source.GetValue(Key, ValueName);
                        if (!(sourceVal is uint) & !(sourceVal is int))
                            return false;
                        try
                        {
                            long num = Convert.ToInt64(sourceVal);
                            return num == Value.NumberValue;
                        }
                        catch
                        {
                            return false;
                        }
                    }

                case PolicyRegistryValueType.Text:
                    {
                        if (!Source.ContainsValue(Key, ValueName))
                            return false;
                        var sourceVal = Source.GetValue(Key, ValueName);
                        if (!(sourceVal is string))
                            return false;
                        return (Convert.ToString(sourceVal) ?? "") == (Value.StringValue ?? "");
                    }

                default:
                    {
                        throw new InvalidOperationException("Illegal value type");
                    }
            }
        }

    private static bool ValueListPresent(PolicyRegistrySingleList ValueList, IPolicySource Source, string Key, string ValueName)
        {
            string sublistKey = string.IsNullOrEmpty(ValueList.DefaultRegistryKey) ? Key : ValueList.DefaultRegistryKey;
            return ValueList.AffectedValues.All(e =>
            {
                string entryKey = string.IsNullOrEmpty(e.RegistryKey) ? sublistKey : e.RegistryKey;
                return ValuePresent(e.Value, Source, entryKey, e.RegistryValue);
            });
        }

        public static int DeduplicatePolicies(AdmxBundle Workspace)
        {
            int dedupeCount = 0;
            foreach (var cat in Workspace.Policies.GroupBy(c => c.Value.Category))
            {
                foreach (var namegroup in cat.GroupBy(p => p.Value.DisplayName).Select(x => x.ToList()).ToList())
                {
                    if (namegroup.Count != 2)
                        continue;
                    var a = namegroup[0].Value;
                    var b = namegroup[1].Value;
                    if ((int)a.RawPolicy.Section + (int)b.RawPolicy.Section != (int)AdmxPolicySection.Both)
                        continue;
                    if ((a.DisplayExplanation ?? "") != (b.DisplayExplanation ?? ""))
                        continue;
                    if ((a.RawPolicy.RegistryKey ?? "") != (b.RawPolicy.RegistryKey ?? ""))
                        continue;
                    a.Category?.Policies.Remove(a);
                    Workspace.Policies.Remove(a.UniqueID);
                    b.RawPolicy.Section = AdmxPolicySection.Both;
                    dedupeCount += 1;
                }
            }

            return dedupeCount;
        }

        public static Dictionary<string, object> GetPolicyOptionStates(IPolicySource PolicySource, PolicyPlusPolicy Policy)
        {
            var state = new Dictionary<string, object>();
            if (Policy.RawPolicy.Elements is null)
                return state;
            foreach (var elem in Policy.RawPolicy.Elements)
            {
                string elemKey = string.IsNullOrEmpty(elem.RegistryKey) ? Policy.RawPolicy.RegistryKey : elem.RegistryKey;
                switch (elem.ElementType ?? "")
                {
                    case "decimal":
                        {
                            try
                            {
                                var raw = PolicySource.GetValue(elemKey, elem.RegistryValue);
                                uint u = Convert.ToUInt32(raw);
                                state.Add(elem.ID, u);
                            }
                            catch
                            {
                                state.Add(elem.ID, 0u);
                            }
                            break;
                        }

                    case "boolean":
                        {
                            BooleanPolicyElement booleanElem = (BooleanPolicyElement)elem;
                            state.Add(elem.ID, GetRegistryListState(PolicySource, booleanElem.AffectedRegistry, elemKey, elem.RegistryValue));
                            break;
                        }

                    case "text":
                        {
                            state.Add(elem.ID, PolicySource.GetValue(elemKey, elem.RegistryValue) ?? string.Empty);
                            break;
                        }

                    case "list":
                        {
                            ListPolicyElement listElem = (ListPolicyElement)elem;
                            if (listElem.UserProvidesNames)
                            {
                                var entries = new Dictionary<string, string>();
                                foreach (var value in PolicySource.GetValueNames(elemKey))
                                    entries.Add(value, Convert.ToString(PolicySource.GetValue(elemKey, value)) ?? string.Empty);
                                state.Add(elem.ID, entries);
                            }
                            else
                            {
                                var entries = new List<string>();
                                if (listElem.HasPrefix)
                                {
                                    int n = 1;
                                    while (PolicySource.ContainsValue(elemKey, elem.RegistryValue + n))
                                    {
                                        entries.Add(Convert.ToString(PolicySource.GetValue(elemKey, elem.RegistryValue + n)) ?? string.Empty);
                                        n += 1;
                                    }
                                }
                                else
                                {
                                    foreach (var value in PolicySource.GetValueNames(elemKey))
                                        entries.Add(value);
                                }

                                state.Add(elem.ID, entries);
                            }

                            break;
                        }

                    case "enum":
                        {
                            EnumPolicyElement enumElem = (EnumPolicyElement)elem;
                            int selectedIndex = -1;
                            uint? numericValue = null;
                            for (int n = 0, loopTo = enumElem.Items.Count - 1; n <= loopTo; n++)
                            {
                                var enumItem = enumElem.Items[n];
                                if (ValuePresent(enumItem.Value, PolicySource, elemKey, elem.RegistryValue))
                                {
                                    if (enumItem.ValueList is null || ValueListPresent(enumItem.ValueList, PolicySource, elemKey, elem.RegistryValue))
                                    {
                                        selectedIndex = n;
                                        if (enumItem.Value != null && enumItem.Value.RegistryType == PolicyRegistryValueType.Numeric)
                                            numericValue = enumItem.Value.NumberValue;
                                        break;
                                    }
                                }
                            }
                            // Prefer returning numeric underlying value; fall back to index if not numeric
                            if (numericValue.HasValue)
                                state.Add(elem.ID, numericValue.Value);
                            else
                                state.Add(elem.ID, selectedIndex);
                            break;
                        }

                    case "multiText":
                        {
                            state.Add(elem.ID, PolicySource.GetValue(elemKey, elem.RegistryValue) ?? Array.Empty<string>());
                            break;
                        }
                }
            }

            return state;
        }

        private static bool GetRegistryListState(IPolicySource PolicySource, PolicyRegistryList RegList, string DefaultKey, string DefaultValueName)
        {
            bool isListAllPresent(PolicyRegistrySingleList l)
            {
                return ValueListPresent(l, PolicySource, DefaultKey, DefaultValueName);
            }

            if (RegList.OnValue is object)
            {
                if (ValuePresent(RegList.OnValue, PolicySource, DefaultKey, DefaultValueName))
                    return true;
            }
            else if (RegList.OnValueList is object)
            {
                if (isListAllPresent(RegList.OnValueList))
                    return true;
            }
            else if (TryGetUInt32(PolicySource.GetValue(DefaultKey, DefaultValueName) ?? 0U) == 1U)
                return true;
            if (RegList.OffValue is object)
            {
                if (ValuePresent(RegList.OffValue, PolicySource, DefaultKey, DefaultValueName))
                    return false;
            }
            else if (RegList.OffValueList is object)
            {
                if (isListAllPresent(RegList.OffValueList))
                    return false;
            }

            return false;
        }

        public static List<RegistryKeyValuePair> GetReferencedRegistryValues(PolicyPlusPolicy Policy)
        {
            return WalkPolicyRegistry(null!, Policy, false);
        }

        public static void ForgetPolicy(IPolicySource PolicySource, PolicyPlusPolicy Policy)
        {
            WalkPolicyRegistry(PolicySource, Policy, true);
        }

    private static List<RegistryKeyValuePair> WalkPolicyRegistry(IPolicySource PolicySource, PolicyPlusPolicy Policy, bool Forget)
        {
            var entries = new List<RegistryKeyValuePair>();
            void addReg(string Key, string Value)
            {
                var rkvp = new RegistryKeyValuePair() { Key = Key, Value = Value };
                if (!entries.Contains(rkvp))
                    entries.Add(rkvp);
            };
            var rawpol = Policy.RawPolicy;
            if (rawpol == null)
                return entries;
            if (!string.IsNullOrEmpty(rawpol.RegistryValue))
                addReg(rawpol.RegistryKey, rawpol.RegistryValue);
            void addSingleList(PolicyRegistrySingleList? SingleList, string OverrideKey)
            {
                if (SingleList is null)
                    return;
                string defaultKey = string.IsNullOrEmpty(OverrideKey) ? rawpol.RegistryKey : OverrideKey;
                string listKey = string.IsNullOrEmpty(SingleList.DefaultRegistryKey) ? defaultKey : SingleList.DefaultRegistryKey;
                foreach (var e in SingleList.AffectedValues)
                {
                    string entryKey = string.IsNullOrEmpty(e.RegistryKey) ? listKey : e.RegistryKey;
                    addReg(entryKey, e.RegistryValue);
                }
            };
            if (rawpol.AffectedValues != null)
            {
                addSingleList(rawpol.AffectedValues.OnValueList, "");
                addSingleList(rawpol.AffectedValues.OffValueList, "");
            }
            if (rawpol.Elements is object)
            {
                foreach (var elem in rawpol.Elements)
                {
                    string elemKey = string.IsNullOrEmpty(elem.RegistryKey) ? rawpol.RegistryKey : elem.RegistryKey;
                    if (elem.ElementType != "list")
                        addReg(elemKey, elem.RegistryValue);
                    switch (elem.ElementType ?? "")
                    {
                        case "boolean":
                            {
                                BooleanPolicyElement booleanElem = (BooleanPolicyElement)elem;
                                if (booleanElem.AffectedRegistry != null)
                                {
                                    addSingleList(booleanElem.AffectedRegistry.OnValueList, elemKey);
                                    addSingleList(booleanElem.AffectedRegistry.OffValueList, elemKey);
                                }
                                break;
                            }

                        case "enum":
                            {
                                EnumPolicyElement enumElem = (EnumPolicyElement)elem;
                                if (enumElem.Items != null)
                                {
                                    foreach (var e in enumElem.Items)
                                    {
                                        if (e.ValueList != null)
                                            addSingleList(e.ValueList, elemKey);
                                    }
                                }
                                break;
                            }

                        case "list":
                            {
                                if (Forget && PolicySource != null)
                                {
                                    PolicySource.ClearKey(elemKey);
                                    PolicySource.ForgetKeyClearance(elemKey);
                                }
                                else
                                {
                                    addReg(elemKey, "");
                                }

                                break;
                            }
                    }
                }
            }

            if (Forget && PolicySource != null)
            {
                foreach (var e in entries)
                    PolicySource.ForgetValue(e.Key, e.Value);
            }

            return entries;
        }

        public static void SetPolicyState(IPolicySource PolicySource, PolicyPlusPolicy Policy, PolicyState State, Dictionary<string, object> Options)
        {
            void setValue(string Key, string ValueName, PolicyRegistryValue? Value)
            {
                if (Value is null)
                    return;
                switch (Value.RegistryType)
                {
                    case PolicyRegistryValueType.Delete:
                        PolicySource.DeleteValue(Key, ValueName);
                        break;
                    case PolicyRegistryValueType.Numeric:
                        PolicySource.SetValue(Key, ValueName, Value.NumberValue, Microsoft.Win32.RegistryValueKind.DWord);
                        break;
                    case PolicyRegistryValueType.Text:
                        PolicySource.SetValue(Key, ValueName, Value.StringValue, Microsoft.Win32.RegistryValueKind.String);
                        break;
                }
            };
            void setSingleList(PolicyRegistrySingleList? SingleList, string DefaultKey)
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
            var rawpol = Policy.RawPolicy;
            switch (State)
            {
                case PolicyState.Enabled:
                    {
                        // Only write the implicit toggle DWORD (1) when the policy has NO element collection.
                        if (rawpol.AffectedValues.OnValue is null & !string.IsNullOrEmpty(rawpol.RegistryValue) && (rawpol.Elements == null || rawpol.Elements.Count == 0))
                            PolicySource.SetValue(rawpol.RegistryKey, rawpol.RegistryValue, 1U, Microsoft.Win32.RegistryValueKind.DWord);
                        setList(rawpol.AffectedValues, rawpol.RegistryKey, rawpol.RegistryValue, true);
                        if (rawpol.Elements is object)
                        {
                            foreach (var elem in rawpol.Elements)
                            {
                                string elemKey = string.IsNullOrEmpty(elem.RegistryKey) ? rawpol.RegistryKey : elem.RegistryKey;
                                if (Options == null || !Options.ContainsKey(elem.ID))
                                {
                                    // If no option supplied but presentation has default, attempt to apply for simple types.
                                    // (Currently defaults are applied at UI layer; here we just skip to avoid overwriting existing values.)
                                    continue;
                                }
                                var optionData = Options[elem.ID];
                                switch (elem.ElementType ?? "")
                                {
                                    case "decimal":
                                        {
                                            DecimalPolicyElement decimalElem = (DecimalPolicyElement)elem;
                                            if (decimalElem.StoreAsText)
                                            {
                                                PolicySource.SetValue(elemKey, elem.RegistryValue, Convert.ToString(optionData) ?? string.Empty, Microsoft.Win32.RegistryValueKind.String);
                                            }
                                            else
                                            {
                                                try
                                                {
                                                    var u = Convert.ToUInt32(optionData);
                                                    PolicySource.SetValue(elemKey, elem.RegistryValue, u, Microsoft.Win32.RegistryValueKind.DWord);
                                                }
                                                catch { }
                                            }

                                            break;
                                        }

                                    case "boolean":
                                        {
                                            BooleanPolicyElement booleanElem = (BooleanPolicyElement)elem;
                                            bool checkState = Convert.ToBoolean(optionData);
                                            if (booleanElem.AffectedRegistry.OnValue is null & checkState)
                                            {
                                                PolicySource.SetValue(elemKey, elem.RegistryValue, 1U, Microsoft.Win32.RegistryValueKind.DWord);
                                            }

                                            if (booleanElem.AffectedRegistry.OffValue is null & !checkState)
                                            {
                                                PolicySource.DeleteValue(elemKey, elem.RegistryValue);
                                            }

                                            setList(booleanElem.AffectedRegistry, elemKey, elem.RegistryValue, checkState);
                                            break;
                                        }

                                    case "text":
                                        {
                                            TextPolicyElement textElem = (TextPolicyElement)elem;
                                            string? rawText = Convert.ToString(optionData) ?? string.Empty;
                                            // Treat whitespace-only as empty for required enforcement
                                            if (string.IsNullOrWhiteSpace(rawText)) rawText = string.Empty;
                                            // Enforce required: if required and empty => skip writing value entirely
                                            if (textElem.Required && string.IsNullOrEmpty(rawText))
                                                break; // do not write
                                            if (rawText.Length > textElem.MaxLength && textElem.MaxLength > 0)
                                                rawText = rawText.Substring(0, textElem.MaxLength);
                                            var regType = textElem.RegExpandSz ? Microsoft.Win32.RegistryValueKind.ExpandString : Microsoft.Win32.RegistryValueKind.String;
                                            PolicySource.SetValue(elemKey, elem.RegistryValue, rawText, regType);
                                            break;
                                        }

                                    case "list":
                                        {
                                            ListPolicyElement listElem = (ListPolicyElement)elem;
                                            if (!listElem.NoPurgeOthers)
                                                PolicySource.ClearKey(elemKey);
                                            if (optionData is null)
                                                continue;
                                            var regType = listElem.RegExpandSz ? Microsoft.Win32.RegistryValueKind.ExpandString : Microsoft.Win32.RegistryValueKind.String;
                                            if (listElem.UserProvidesNames)
                                            {
                                                // Accept list of KVPs or dictionary
                                                if (optionData is List<KeyValuePair<string, string>> kvps)
                                                {
                                                    foreach (var i in kvps)
                                                        PolicySource.SetValue(elemKey, i.Key, i.Value, regType);
                                                }
                                                else if (optionData is IEnumerable<KeyValuePair<string, string>> kvpEnum)
                                                {
                                                    foreach (var i in kvpEnum)
                                                        PolicySource.SetValue(elemKey, i.Key, i.Value, regType);
                                                }
                                                else if (optionData is Dictionary<string, string> dict)
                                                {
                                                    foreach (var i in dict)
                                                        PolicySource.SetValue(elemKey, i.Key, i.Value, regType);
                                                }
                                            }
                                            else
                                            {
                                                List<string> items = (optionData as IEnumerable<string>)?.ToList() ?? new List<string>();
                                                int n = 1;
                                                while (n <= items.Count)
                                                {
                                                    string valueName = listElem.HasPrefix ? listElem.RegistryValue + n : items[n - 1];
                                                    PolicySource.SetValue(elemKey, valueName, items[n - 1], regType);
                                                    n += 1;
                                                }
                                            }

                                            break;
                                        }

                                    case "enum":
                                        {
                                            EnumPolicyElement enumElem = (EnumPolicyElement)elem;
                                            int selIndex = -1;
                                            try { selIndex = Convert.ToInt32(optionData); } catch { selIndex = -1; }
                                            if (selIndex < 0 || selIndex >= enumElem.Items.Count)
                                            {
                                                // If required, fallback to first item; else skip
                                                if (enumElem.Required && enumElem.Items.Count > 0)
                                                    selIndex = 0;
                                                else
                                                    break;
                                            }
                                            var selItem = enumElem.Items[selIndex];
                                            setValue(elemKey, elem.RegistryValue, selItem.Value);
                                            if (selItem.ValueList != null)
                                                setSingleList(selItem.ValueList, elemKey);
                                            break;
                                        }

                                    case "multiText":
                                        {
                                            if (optionData is string[] multiLines)
                                                PolicySource.SetValue(elemKey, elem.RegistryValue, multiLines, Microsoft.Win32.RegistryValueKind.MultiString);
                                            else if (optionData is IEnumerable<string> lines)
                                                PolicySource.SetValue(elemKey, elem.RegistryValue, lines.ToArray(), Microsoft.Win32.RegistryValueKind.MultiString);
                                            else if (optionData != null)
                                                PolicySource.SetValue(elemKey, elem.RegistryValue, new[] { optionData.ToString() }, Microsoft.Win32.RegistryValueKind.MultiString);
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
                            PolicySource.DeleteValue(rawpol.RegistryKey, rawpol.RegistryValue);
                        setList(rawpol.AffectedValues, rawpol.RegistryKey, rawpol.RegistryValue, false);
                        if (rawpol.Elements is object)
                        {
                            foreach (var elem in rawpol.Elements)
                            {
                                string elemKey = string.IsNullOrEmpty(elem.RegistryKey) ? rawpol.RegistryKey : elem.RegistryKey;
                                if (elem.ElementType == "list")
                                {
                                    PolicySource.ClearKey(elemKey);
                                }
                                else if (elem.ElementType == "boolean")
                                {
                                    BooleanPolicyElement booleanElem = (BooleanPolicyElement)elem;
                                    if (booleanElem.AffectedRegistry.OffValue is object | booleanElem.AffectedRegistry.OffValueList is object)
                                    {
                                        setList(booleanElem.AffectedRegistry, elemKey, elem.RegistryValue, false);
                                    }
                                    else
                                    {
                                        PolicySource.DeleteValue(elemKey, elem.RegistryValue);
                                    }
                                }
                                else
                                {
                                    PolicySource.DeleteValue(elemKey, elem.RegistryValue);
                                }
                            }
                        }

                        break;
                    }
            }
        }

        public static bool IsPolicySupported(PolicyPlusPolicy Policy, List<PolicyPlusProduct> Products, bool AlwaysUseAny, bool ApproveLiterals)
        {
            if (Policy.SupportedOn is null || Policy.SupportedOn.RawSupport.Logic == AdmxSupportLogicType.Blank)
                return ApproveLiterals;
            bool supEntryMet(PolicyPlusSupportEntry SupportEntry)
            {
                if (SupportEntry.Product is null)
                    return ApproveLiterals;
                if (Products.Contains(SupportEntry.Product) & !SupportEntry.RawSupportEntry.IsRange)
                    return true;
                if (SupportEntry.Product.Children is null || SupportEntry.Product.Children.Count == 0)
                    return false;
                int rangeMin = SupportEntry.RawSupportEntry.MinVersion ?? 0;
                int rangeMax = SupportEntry.RawSupportEntry.MaxVersion ?? SupportEntry.Product.Children.Max(p => p.RawProduct.Version);
                for (int v = rangeMin, loopTo = rangeMax; v <= loopTo; v++)
                {
                    int version = v;
                    var subproduct = SupportEntry.Product.Children.FirstOrDefault(p => p.RawProduct.Version == version);
                    if (subproduct is null)
                        continue;
                    if (Products.Contains(subproduct))
                        return true;
                    if (subproduct.Children is object && subproduct.Children.Any(p => Products.Contains(p)))
                        return true;
                }

                return false;
            };
            var entriesSeen = new List<PolicyPlusSupport>();

            bool supDefMet(PolicyPlusSupport Support)
            {
                if (entriesSeen.Contains(Support))
                    return false;
                entriesSeen.Add(Support);
                bool requireAll = AlwaysUseAny ? Support.RawSupport.Logic == AdmxSupportLogicType.AllOf : false;
                foreach (var supElem in Support.Elements.Where(e => e.SupportDefinition is null))
                {
                    bool isMet = supEntryMet(supElem);
                    if (requireAll)
                    {
                        if (!isMet)
                            return false;
                    }
                    else if (isMet)
                        return true;
                }

                foreach (var subDef in Support.Elements.Where(e => e.SupportDefinition is object))
                {
                    bool isMet = subDef.SupportDefinition != null && supDefMet(subDef.SupportDefinition);
                    if (requireAll)
                    {
                        if (!isMet)
                            return false;
                    }
                    else if (isMet)
                        return true;
                }

                return requireAll;
            }

            return supDefMet(Policy.SupportedOn);
        }
    }

    public enum PolicyState
    {
        NotConfigured = 0,
        Disabled = 1,
        Enabled = 2,
        Unknown = 3
    }

    public class RegistryKeyValuePair : IEquatable<RegistryKeyValuePair?>
    {
        public string Key = string.Empty;
        public string Value = string.Empty;

        bool IEquatable<RegistryKeyValuePair?>.Equals(RegistryKeyValuePair? other)
        {
            if (other is null) return false;
            return other.Key.Equals(Key, StringComparison.InvariantCultureIgnoreCase) & other.Value.Equals(Value, StringComparison.InvariantCultureIgnoreCase);
        }

        public bool EqualsRKVP(RegistryKeyValuePair? other)
        {
            return ((IEquatable<RegistryKeyValuePair>)this).Equals(other);
        }

        public override bool Equals(object? obj)
        {
            if (!(obj is RegistryKeyValuePair))
                return false;
            return EqualsRKVP((RegistryKeyValuePair)obj);
        }

        public override int GetHashCode()
        {
            return Key.ToLowerInvariant().GetHashCode() ^ Value.ToLowerInvariant().GetHashCode();
        }
    }

    public class Registry : RegistryKeyValuePair
    {
    public string Type = string.Empty;
    }
}
