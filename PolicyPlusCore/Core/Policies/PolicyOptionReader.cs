using System.Globalization;
using PolicyPlusCore.IO;

namespace PolicyPlusCore.Core.Policies
{
    /// <summary>
    /// Reads element option values from a policy source.
    /// Extracted from PolicyProcessing.GetPolicyOptionStates (ADR 0013 Phase 3a).
    /// </summary>
    internal static class PolicyOptionReader
    {
        /// <summary>
        /// Retrieves the current option states (element values) for a policy.
        /// </summary>
        /// <param name="policySource">The registry/POL source to read from.</param>
        /// <param name="policy">The policy to read options for.</param>
        /// <returns>Dictionary of element ID to current value.</returns>
        public static Dictionary<string, object> GetOptionStates(
            IPolicySource policySource,
            PolicyPlusPolicy policy
        )
        {
            var state = new Dictionary<string, object>();

            if (policy.RawPolicy.Elements is null)
                return state;

            foreach (var elem in policy.RawPolicy.Elements)
            {
                string elemKey = string.IsNullOrEmpty(elem.RegistryKey)
                    ? policy.RawPolicy.RegistryKey
                    : elem.RegistryKey;

                ReadElementValue(policySource, elem, elemKey, state);
            }

            return state;
        }

        private static void ReadElementValue(
            IPolicySource policySource,
            PolicyElement elem,
            string elemKey,
            Dictionary<string, object> state
        )
        {
            switch (elem.ElementType ?? "")
            {
                case "decimal":
                    ReadDecimalElement(policySource, elem, elemKey, state);
                    break;

                case "boolean":
                    ReadBooleanElement(policySource, (BooleanPolicyElement)elem, elemKey, state);
                    break;

                case "text":
                    ReadTextElement(policySource, elem, elemKey, state);
                    break;

                case "list":
                    ReadListElement(policySource, (ListPolicyElement)elem, elemKey, state);
                    break;

                case "enum":
                    ReadEnumElement(policySource, (EnumPolicyElement)elem, elemKey, state);
                    break;

                case "multiText":
                    ReadMultiTextElement(policySource, elem, elemKey, state);
                    break;
            }
        }

        private static void ReadDecimalElement(
            IPolicySource policySource,
            PolicyElement elem,
            string elemKey,
            Dictionary<string, object> state
        )
        {
            try
            {
                var raw = policySource.GetValue(elemKey, elem.RegistryValue);
                uint u = Convert.ToUInt32(raw);
                state.Add(elem.ID, u);
            }
            catch
            {
                state.Add(elem.ID, 0u);
            }
        }

        private static void ReadBooleanElement(
            IPolicySource policySource,
            BooleanPolicyElement booleanElem,
            string elemKey,
            Dictionary<string, object> state
        )
        {
            state.Add(
                booleanElem.ID,
                GetRegistryListState(
                    policySource,
                    booleanElem.AffectedRegistry,
                    elemKey,
                    booleanElem.RegistryValue
                )
            );
        }

        private static void ReadTextElement(
            IPolicySource policySource,
            PolicyElement elem,
            string elemKey,
            Dictionary<string, object> state
        )
        {
            state.Add(elem.ID, policySource.GetValue(elemKey, elem.RegistryValue) ?? string.Empty);
        }

        private static void ReadListElement(
            IPolicySource policySource,
            ListPolicyElement listElem,
            string elemKey,
            Dictionary<string, object> state
        )
        {
            if (listElem.UserProvidesNames)
            {
                var entries = new Dictionary<string, string>();
                foreach (var value in policySource.GetValueNames(elemKey))
                {
                    entries.Add(
                        value,
                        Convert.ToString(policySource.GetValue(elemKey, value)) ?? string.Empty
                    );
                }
                state.Add(listElem.ID, entries);
            }
            else
            {
                var entries = new List<string>();
                if (listElem.HasPrefix)
                {
                    // Enumerate contiguous numeric-suffixed values (prefix + 1..n) and stop at first gap.
                    int n = 1;
                    while (true)
                    {
                        string name = listElem.RegistryValue + n;
                        if (policySource.ContainsValue(elemKey, name))
                        {
                            entries.Add(
                                Convert.ToString(policySource.GetValue(elemKey, name))
                                    ?? string.Empty
                            );
                            n++;
                            continue;
                        }
                        break; // first missing index terminates sequence per ADMX list semantics
                    }
                }
                else
                {
                    foreach (var value in policySource.GetValueNames(elemKey))
                        entries.Add(value);
                }

                state.Add(listElem.ID, entries);
            }
        }

        private static void ReadEnumElement(
            IPolicySource policySource,
            EnumPolicyElement enumElem,
            string elemKey,
            Dictionary<string, object> state
        )
        {
            int selectedIndex = -1;

            for (int n = 0; n < enumElem.Items.Count; n++)
            {
                var enumItem = enumElem.Items[n];
                if (
                    PolicyStateEvaluator.ValuePresent(
                        enumItem.Value,
                        policySource,
                        elemKey,
                        enumElem.RegistryValue
                    )
                )
                {
                    if (
                        enumItem.ValueList is null
                        || PolicyStateEvaluator.ValueListPresent(
                            enumItem.ValueList,
                            policySource,
                            elemKey,
                            enumElem.RegistryValue
                        )
                    )
                    {
                        selectedIndex = n;
                        break;
                    }
                }
            }

            state.Add(enumElem.ID, selectedIndex);
        }

        private static void ReadMultiTextElement(
            IPolicySource policySource,
            PolicyElement elem,
            string elemKey,
            Dictionary<string, object> state
        )
        {
            state.Add(
                elem.ID,
                policySource.GetValue(elemKey, elem.RegistryValue) ?? Array.Empty<string>()
            );
        }

        /// <summary>
        /// Determines the boolean state of a registry list (On/Off value matching).
        /// </summary>
        internal static bool GetRegistryListState(
            IPolicySource policySource,
            PolicyRegistryList regList,
            string defaultKey,
            string defaultValueName
        )
        {
            if (regList.OnValue is object)
            {
                if (
                    PolicyStateEvaluator.ValuePresent(
                        regList.OnValue,
                        policySource,
                        defaultKey,
                        defaultValueName
                    )
                )
                    return true;
            }
            else if (regList.OnValueList is object)
            {
                if (
                    PolicyStateEvaluator.ValueListPresent(
                        regList.OnValueList,
                        policySource,
                        defaultKey,
                        defaultValueName
                    )
                )
                    return true;
            }
            else if (TryGetUInt32(policySource.GetValue(defaultKey, defaultValueName) ?? 0U) == 1U)
            {
                return true;
            }

            if (regList.OffValue is object)
            {
                if (
                    PolicyStateEvaluator.ValuePresent(
                        regList.OffValue,
                        policySource,
                        defaultKey,
                        defaultValueName
                    )
                )
                    return false;
            }
            else if (regList.OffValueList is object)
            {
                if (
                    PolicyStateEvaluator.ValueListPresent(
                        regList.OffValueList,
                        policySource,
                        defaultKey,
                        defaultValueName
                    )
                )
                    return false;
            }

            return false;
        }

        /// <summary>
        /// Attempts to parse a value as UInt32 with various format support.
        /// </summary>
        internal static uint TryGetUInt32(object value)
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

                    if (
                        s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                        || s.StartsWith("&H", StringComparison.OrdinalIgnoreCase)
                    )
                    {
                        var hex = s.Substring(2);
                        if (
                            uint.TryParse(
                                hex,
                                NumberStyles.HexNumber,
                                CultureInfo.InvariantCulture,
                                out uint parsed
                            )
                        )
                            return parsed;
                    }

                    if (
                        uint.TryParse(
                            s,
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out uint parsedInt
                        )
                    )
                        return parsedInt;

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
    }
}
