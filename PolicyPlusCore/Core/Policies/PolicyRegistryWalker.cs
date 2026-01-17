using PolicyPlusCore.IO;

namespace PolicyPlusCore.Core.Policies
{
    /// <summary>
    /// Enumerates registry keys/values referenced by a policy and handles policy forgetting.
    /// Extracted from PolicyProcessing.WalkPolicyRegistry (ADR 0013 Phase 3b).
    /// </summary>
    internal static class PolicyRegistryWalker
    {
        /// <summary>
        /// Gets all registry key/value pairs referenced by a policy.
        /// </summary>
        public static List<RegistryKeyValuePair> GetReferencedValues(PolicyPlusPolicy policy)
        {
            return Walk(null!, policy, forget: false);
        }

        /// <summary>
        /// Clears all registry values associated with a policy (makes it "forgotten").
        /// </summary>
        public static void Forget(IPolicySource policySource, PolicyPlusPolicy policy)
        {
            Walk(policySource, policy, forget: true);
        }

        /// <summary>
        /// Core registry walking logic. When forget=true, clears values from the source.
        /// </summary>
        internal static List<RegistryKeyValuePair> Walk(
            IPolicySource policySource,
            PolicyPlusPolicy policy,
            bool forget
        )
        {
            var entries = new List<RegistryKeyValuePair>();

            void addReg(string key, string value)
            {
                var rkvp = new RegistryKeyValuePair { Key = key, Value = value };
                if (!entries.Contains(rkvp))
                    entries.Add(rkvp);
            }

            var rawpol = policy.RawPolicy;
            if (rawpol == null)
                return entries;

            if (!string.IsNullOrEmpty(rawpol.RegistryValue))
                addReg(rawpol.RegistryKey, rawpol.RegistryValue);

            void addSingleList(PolicyRegistrySingleList? singleList, string overrideKey)
            {
                if (singleList is null)
                    return;

                string defaultKey = string.IsNullOrEmpty(overrideKey)
                    ? rawpol.RegistryKey
                    : overrideKey;

                string listKey = string.IsNullOrEmpty(singleList.DefaultRegistryKey)
                    ? defaultKey
                    : singleList.DefaultRegistryKey;

                foreach (var e in singleList.AffectedValues)
                {
                    string entryKey = string.IsNullOrEmpty(e.RegistryKey) ? listKey : e.RegistryKey;
                    addReg(entryKey, e.RegistryValue);
                }
            }

            if (rawpol.AffectedValues != null)
            {
                addSingleList(rawpol.AffectedValues.OnValueList, "");
                addSingleList(rawpol.AffectedValues.OffValueList, "");
            }

            if (rawpol.Elements is object)
            {
                foreach (var elem in rawpol.Elements)
                {
                    string elemKey = string.IsNullOrEmpty(elem.RegistryKey)
                        ? rawpol.RegistryKey
                        : elem.RegistryKey;

                    if (elem.ElementType != "list")
                        addReg(elemKey, elem.RegistryValue);

                    switch (elem.ElementType ?? "")
                    {
                        case "boolean":
                        {
                            var booleanElem = (BooleanPolicyElement)elem;
                            if (booleanElem.AffectedRegistry != null)
                            {
                                addSingleList(booleanElem.AffectedRegistry.OnValueList, elemKey);
                                addSingleList(booleanElem.AffectedRegistry.OffValueList, elemKey);
                            }
                            break;
                        }

                        case "enum":
                        {
                            var enumElem = (EnumPolicyElement)elem;
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
                            if (forget && policySource != null)
                            {
                                policySource.ClearKey(elemKey);
                                policySource.ForgetKeyClearance(elemKey);
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

            if (forget && policySource != null)
            {
                foreach (var e in entries)
                    policySource.ForgetValue(e.Key, e.Value);
            }

            return entries;
        }
    }
}
