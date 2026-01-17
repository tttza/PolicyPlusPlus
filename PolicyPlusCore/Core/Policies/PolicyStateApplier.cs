using PolicyPlusCore.IO;

namespace PolicyPlusCore.Core.Policies
{
    /// <summary>
    /// Applies a policy state (Enabled/Disabled) to a policy source.
    /// Extracted from PolicyProcessing.SetPolicyState (ADR 0013 Phase 2).
    /// </summary>
    internal static class PolicyStateApplier
    {
        private static void LogDebug(string area, string message)
        {
#if DEBUG
            System.Diagnostics.Debug.WriteLine($"[{area}] {message}");
#endif
        }

        private static void LogError(string area, string message, Exception ex)
        {
#if DEBUG
            System.Diagnostics.Debug.WriteLine($"[{area}] ERROR: {message} - {ex.Message}");
#endif
        }

        /// <summary>
        /// Apply the given policy state to the policy source.
        /// </summary>
        /// <param name="policySource">The target registry/POL source.</param>
        /// <param name="policy">The policy to apply.</param>
        /// <param name="state">The desired state (Enabled, Disabled, or NotConfigured).</param>
        /// <param name="options">Element option values (element ID → value).</param>
        public static void Apply(
            IPolicySource policySource,
            PolicyPlusPolicy policy,
            PolicyState state,
            Dictionary<string, object> options
        )
        {
            // Record that this policy has been explicitly configured (Enabled/Disabled).
            try
            {
                if (
                    policy != null
                    && (state == PolicyState.Enabled || state == PolicyState.Disabled)
                )
                {
                    var raw = policy.RawPolicy;
                    if (raw != null)
                    {
                        var section = raw.Section;
                        string scope = section == AdmxPolicySection.User ? "User" : "Computer";
                        var pid = policy.UniqueID ?? string.Empty;
                        if (!string.IsNullOrEmpty(pid))
                            ConfiguredPolicyTracker.MarkConfigured(pid, scope);
                    }
                }
            }
            catch { }

            if (policy == null)
                return;
            if (policy.RawPolicy is not AdmxPolicy rawPolicy)
                return;

            // Ensure we work with a non-null registry list reference
            var affectedValues = rawPolicy.AffectedValues;
            if (affectedValues == null)
            {
                affectedValues = new PolicyRegistryList();
                rawPolicy.AffectedValues = affectedValues;
            }

            switch (state)
            {
                case PolicyState.Enabled:
                    ApplyEnabledState(policySource, rawPolicy, affectedValues, options);
                    break;

                case PolicyState.Disabled:
                    ApplyDisabledState(policySource, rawPolicy, affectedValues);
                    break;
            }
        }

        private static void ApplyEnabledState(
            IPolicySource policySource,
            AdmxPolicy rawPolicy,
            PolicyRegistryList affectedValues,
            Dictionary<string, object> options
        )
        {
            // Default OnValue if not specified
            if (affectedValues.OnValue is null && !string.IsNullOrEmpty(rawPolicy.RegistryValue))
            {
                policySource.SetValue(
                    rawPolicy.RegistryKey,
                    rawPolicy.RegistryValue,
                    1U,
                    Microsoft.Win32.RegistryValueKind.DWord
                );
            }

            SetList(
                policySource,
                affectedValues,
                rawPolicy.RegistryKey,
                rawPolicy.RegistryValue,
                true
            );

            if (rawPolicy.Elements is null)
                return;

            foreach (var elem in rawPolicy.Elements)
            {
                string elemKey = string.IsNullOrEmpty(elem.RegistryKey)
                    ? rawPolicy.RegistryKey
                    : elem.RegistryKey;

                if (options == null || !options.ContainsKey(elem.ID))
                    continue;

                var optionData = options[elem.ID];
                ApplyElementEnabled(policySource, elem, elemKey, optionData);
            }
        }

        private static void ApplyElementEnabled(
            IPolicySource policySource,
            PolicyElement elem,
            string elemKey,
            object optionData
        )
        {
            switch (elem.ElementType ?? "")
            {
                case "decimal":
                    ApplyDecimalElement(
                        policySource,
                        (DecimalPolicyElement)elem,
                        elemKey,
                        optionData
                    );
                    break;

                case "boolean":
                    ApplyBooleanElementEnabled(
                        policySource,
                        (BooleanPolicyElement)elem,
                        elemKey,
                        optionData
                    );
                    break;

                case "text":
                    ApplyTextElement(policySource, (TextPolicyElement)elem, elemKey, optionData);
                    break;

                case "list":
                    ApplyListElement(policySource, (ListPolicyElement)elem, elemKey, optionData);
                    break;

                case "enum":
                    ApplyEnumElement(policySource, (EnumPolicyElement)elem, elemKey, optionData);
                    break;

                case "multiText":
                    ApplyMultiTextElement(policySource, elem, elemKey, optionData);
                    break;
            }
        }

        private static void ApplyDecimalElement(
            IPolicySource policySource,
            DecimalPolicyElement decimalElem,
            string elemKey,
            object optionData
        )
        {
            if (decimalElem.StoreAsText)
            {
                policySource.SetValue(
                    elemKey,
                    decimalElem.RegistryValue,
                    Convert.ToString(optionData) ?? string.Empty,
                    Microsoft.Win32.RegistryValueKind.String
                );
            }
            else
            {
                try
                {
                    var u = Convert.ToUInt32(optionData);
                    policySource.SetValue(
                        elemKey,
                        decimalElem.RegistryValue,
                        u,
                        Microsoft.Win32.RegistryValueKind.DWord
                    );
                }
                catch { }
            }
        }

        private static void ApplyBooleanElementEnabled(
            IPolicySource policySource,
            BooleanPolicyElement booleanElem,
            string elemKey,
            object optionData
        )
        {
            bool checkState = Convert.ToBoolean(optionData);

            if (booleanElem.AffectedRegistry.OnValue is null && checkState)
            {
                policySource.SetValue(
                    elemKey,
                    booleanElem.RegistryValue,
                    1U,
                    Microsoft.Win32.RegistryValueKind.DWord
                );
            }

            if (booleanElem.AffectedRegistry.OffValue is null && !checkState)
            {
                policySource.DeleteValue(elemKey, booleanElem.RegistryValue);
            }

            SetList(
                policySource,
                booleanElem.AffectedRegistry,
                elemKey,
                booleanElem.RegistryValue,
                checkState
            );
        }

        private static void ApplyTextElement(
            IPolicySource policySource,
            TextPolicyElement textElem,
            string elemKey,
            object optionData
        )
        {
            string? rawText = Convert.ToString(optionData) ?? string.Empty;
            if (rawText.Length > textElem.MaxLength && textElem.MaxLength > 0)
                rawText = rawText.Substring(0, textElem.MaxLength);

            var regType = textElem.RegExpandSz
                ? Microsoft.Win32.RegistryValueKind.ExpandString
                : Microsoft.Win32.RegistryValueKind.String;

            policySource.SetValue(elemKey, textElem.RegistryValue, rawText, regType);
        }

        private static void ApplyListElement(
            IPolicySource policySource,
            ListPolicyElement listElem,
            string elemKey,
            object optionData
        )
        {
            if (!listElem.NoPurgeOthers)
                policySource.ClearKey(elemKey);

            if (optionData is null)
                return;

            var regType = listElem.RegExpandSz
                ? Microsoft.Win32.RegistryValueKind.ExpandString
                : Microsoft.Win32.RegistryValueKind.String;

            if (listElem.UserProvidesNames)
            {
                if (optionData is List<KeyValuePair<string, string>> kvps)
                {
                    foreach (var i in kvps)
                        policySource.SetValue(elemKey, i.Key, i.Value, regType);
                }
                else if (optionData is IEnumerable<KeyValuePair<string, string>> kvpEnum)
                {
                    foreach (var i in kvpEnum)
                        policySource.SetValue(elemKey, i.Key, i.Value, regType);
                }
                else if (optionData is Dictionary<string, string> dict)
                {
                    foreach (var i in dict)
                        policySource.SetValue(elemKey, i.Key, i.Value, regType);
                }
            }
            else
            {
                List<string> items =
                    (optionData as IEnumerable<string>)?.ToList() ?? new List<string>();
                int n = 1;
                while (n <= items.Count)
                {
                    string valueName = listElem.HasPrefix
                        ? listElem.RegistryValue + n
                        : items[n - 1];
                    policySource.SetValue(elemKey, valueName, items[n - 1], regType);
                    n += 1;
                }
            }
        }

        private static void ApplyEnumElement(
            IPolicySource policySource,
            EnumPolicyElement enumElem,
            string elemKey,
            object optionData
        )
        {
            int selIndex = -1;
            try
            {
                selIndex = Convert.ToInt32(optionData);
            }
            catch
            {
                selIndex = -1;
            }

            if (selIndex < 0 || selIndex >= enumElem.Items.Count)
            {
                if (enumElem.Required && enumElem.Items.Count > 0)
                    selIndex = 0;
                else
                    return;
            }

            var selItem = enumElem.Items[selIndex];
            SetValue(policySource, elemKey, enumElem.RegistryValue, selItem.Value);

            if (selItem.ValueList != null)
                SetSingleList(policySource, selItem.ValueList, elemKey);
        }

        private static void ApplyMultiTextElement(
            IPolicySource policySource,
            PolicyElement elem,
            string elemKey,
            object optionData
        )
        {
            if (optionData is string[] multiLines)
            {
                policySource.SetValue(
                    elemKey,
                    elem.RegistryValue,
                    multiLines,
                    Microsoft.Win32.RegistryValueKind.MultiString
                );
            }
            else if (optionData is IEnumerable<string> lines)
            {
                policySource.SetValue(
                    elemKey,
                    elem.RegistryValue,
                    lines.ToArray(),
                    Microsoft.Win32.RegistryValueKind.MultiString
                );
            }
            else if (optionData != null)
            {
                policySource.SetValue(
                    elemKey,
                    elem.RegistryValue,
                    new[] { optionData.ToString() },
                    Microsoft.Win32.RegistryValueKind.MultiString
                );
            }
        }

        private static void ApplyDisabledState(
            IPolicySource policySource,
            AdmxPolicy rawPolicy,
            PolicyRegistryList affectedValues
        )
        {
            // Default OffValue behavior: delete if not specified
            if (affectedValues.OffValue is null && !string.IsNullOrEmpty(rawPolicy.RegistryValue))
                policySource.DeleteValue(rawPolicy.RegistryKey, rawPolicy.RegistryValue);

            SetList(
                policySource,
                affectedValues,
                rawPolicy.RegistryKey,
                rawPolicy.RegistryValue,
                false
            );

            if (rawPolicy.Elements is null)
                return;

            foreach (var elem in rawPolicy.Elements)
            {
                string elemKey = string.IsNullOrEmpty(elem.RegistryKey)
                    ? rawPolicy.RegistryKey
                    : elem.RegistryKey;

                if (elem.ElementType == "list")
                {
                    policySource.ClearKey(elemKey);
                }
                else if (elem.ElementType == "boolean")
                {
                    BooleanPolicyElement booleanElem = (BooleanPolicyElement)elem;
                    if (
                        booleanElem.AffectedRegistry.OffValue is object
                        || booleanElem.AffectedRegistry.OffValueList is object
                    )
                    {
                        SetList(
                            policySource,
                            booleanElem.AffectedRegistry,
                            elemKey,
                            elem.RegistryValue,
                            false
                        );
                    }
                    else
                    {
                        policySource.DeleteValue(elemKey, elem.RegistryValue);
                    }
                }
                else
                {
                    policySource.DeleteValue(elemKey, elem.RegistryValue);
                }
            }
        }

        #region Registry Value Helpers

        private static void SetValue(
            IPolicySource policySource,
            string key,
            string valueName,
            PolicyRegistryValue? value
        )
        {
            if (value is null)
                return;

            switch (value.RegistryType)
            {
                case PolicyRegistryValueType.Delete:
                    policySource.DeleteValue(key, valueName);
                    break;

                case PolicyRegistryValueType.Numeric:
                    policySource.SetValue(
                        key,
                        valueName,
                        value.NumberValue,
                        Microsoft.Win32.RegistryValueKind.DWord
                    );
                    break;

                case PolicyRegistryValueType.Text:
                    policySource.SetValue(
                        key,
                        valueName,
                        value.StringValue,
                        Microsoft.Win32.RegistryValueKind.String
                    );
                    break;
            }
        }

        private static void SetSingleList(
            IPolicySource policySource,
            PolicyRegistrySingleList? singleList,
            string defaultKey
        )
        {
            if (singleList is null)
                return;

            string listKey = string.IsNullOrEmpty(singleList.DefaultRegistryKey)
                ? defaultKey
                : singleList.DefaultRegistryKey;

            foreach (var e in singleList.AffectedValues)
            {
                string itemKey = string.IsNullOrEmpty(e.RegistryKey) ? listKey : e.RegistryKey;
                SetValue(policySource, itemKey, e.RegistryValue, e.Value);
            }
        }

        private static void SetList(
            IPolicySource policySource,
            PolicyRegistryList list,
            string defaultKey,
            string defaultValue,
            bool isOn
        )
        {
            if (list is null)
                return;

            if (isOn)
            {
                SetValue(policySource, defaultKey, defaultValue, list.OnValue);
                SetSingleList(policySource, list.OnValueList, defaultKey);
            }
            else
            {
                SetValue(policySource, defaultKey, defaultValue, list.OffValue);
                SetSingleList(policySource, list.OffValueList, defaultKey);
            }
        }

        #endregion
    }
}
