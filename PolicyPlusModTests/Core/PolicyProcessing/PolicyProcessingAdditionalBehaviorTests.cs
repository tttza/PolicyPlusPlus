using System.Collections.Generic;
using System.Linq;
using PolicyPlusCore.Admx;
using PolicyPlusCore.Core;
using Xunit;

namespace PolicyPlusModTests.Core.PolicyProcessingAdditional
{
    public class PolicyProcessingAdditionalBehaviorTests
    {
        private sealed class InMem : IPolicySource
        {
            private readonly PolFile _pol = new PolFile();

            public bool ContainsValue(string Key, string Value) => _pol.ContainsValue(Key, Value);

            public object? GetValue(string Key, string Value) => _pol.GetValue(Key, Value);

            public bool WillDeleteValue(string Key, string Value) =>
                _pol.WillDeleteValue(Key, Value);

            public List<string> GetValueNames(string Key) => _pol.GetValueNames(Key);

            public void SetValue(
                string Key,
                string Value,
                object Data,
                Microsoft.Win32.RegistryValueKind DataType
            ) => _pol.SetValue(Key, Value, Data, DataType);

            public void ForgetValue(string Key, string Value) => _pol.ForgetValue(Key, Value);

            public void DeleteValue(string Key, string Value) => _pol.DeleteValue(Key, Value);

            public void ClearKey(string Key) => _pol.ClearKey(Key);

            public void ForgetKeyClearance(string Key) => _pol.ForgetKeyClearance(Key);

            public PolFile Inner => _pol;
        }

        private static AdmxFile Dummy() => new AdmxFile { SourceFile = "dummy.admx" };

        [Fact(DisplayName = "Boolean OffValue Delete vs Numeric writes correct action")]
        public void Boolean_OffValue_DeleteVsNumeric()
        {
            var offDelete = new BooleanPolicyElement
            {
                ID = "BDel",
                ElementType = "boolean",
                RegistryKey = "Software\\PolicyPlusTest",
                RegistryValue = "FlagDel",
                AffectedRegistry = new PolicyRegistryList
                {
                    OffValue = new PolicyRegistryValue
                    {
                        RegistryType = PolicyRegistryValueType.Delete,
                    },
                    OnValue = new PolicyRegistryValue
                    {
                        RegistryType = PolicyRegistryValueType.Numeric,
                        NumberValue = 1U,
                    },
                },
            };
            var offNumeric = new BooleanPolicyElement
            {
                ID = "BNum",
                ElementType = "boolean",
                RegistryKey = "Software\\PolicyPlusTest",
                RegistryValue = "FlagNum",
                AffectedRegistry = new PolicyRegistryList
                {
                    OffValue = new PolicyRegistryValue
                    {
                        RegistryType = PolicyRegistryValueType.Numeric,
                        NumberValue = 0U,
                    },
                    OnValue = new PolicyRegistryValue
                    {
                        RegistryType = PolicyRegistryValueType.Numeric,
                        NumberValue = 1U,
                    },
                },
            };
            var raw = new AdmxPolicy
            {
                RegistryKey = "Software\\PolicyPlusTest",
                RegistryValue = "Root",
                Section = AdmxPolicySection.Machine,
                Elements = new List<PolicyElement> { offDelete, offNumeric },
                AffectedValues = new PolicyRegistryList(),
                DefinedIn = Dummy(),
            };
            var pol = new PolicyPlusPolicy
            {
                UniqueID = "M:BoolOff",
                DisplayName = "BoolOff",
                RawPolicy = raw,
            };
            var src = new InMem();
            // Enabled both flags first
            global::PolicyPlusCore.Core.PolicyProcessing.SetPolicyState(
                src,
                pol,
                PolicyState.Enabled,
                new Dictionary<string, object> { { "BDel", true }, { "BNum", true } }
            );
            Assert.Equal(1U, src.GetValue("Software\\PolicyPlusTest", "FlagDel"));
            Assert.Equal(1U, src.GetValue("Software\\PolicyPlusTest", "FlagNum"));
            // Disable both
            global::PolicyPlusCore.Core.PolicyProcessing.SetPolicyState(
                src,
                pol,
                PolicyState.Enabled,
                new Dictionary<string, object> { { "BDel", false }, { "BNum", false } }
            );
            // Delete case removed value; numeric case wrote 0
            Assert.False(src.ContainsValue("Software\\PolicyPlusTest", "FlagDel"));
            Assert.Equal(0U, src.GetValue("Software\\PolicyPlusTest", "FlagNum"));
        }

        [Fact(DisplayName = "Evidence weighting: root OnValue beats 0.1 disabled element evidence")]
        public void EvidenceWeighting_RootBeatsFractional()
        {
            // root On numeric 1; boolean OffValue triggers 0.1 disabled evidence but should not override explicit root
            var boolElem = new BooleanPolicyElement
            {
                ID = "B",
                ElementType = "boolean",
                RegistryKey = "Software\\PolicyPlusTest",
                RegistryValue = "Flag",
                AffectedRegistry = new PolicyRegistryList
                {
                    OffValue = new PolicyRegistryValue
                    {
                        RegistryType = PolicyRegistryValueType.Numeric,
                        NumberValue = 0U,
                    },
                },
            };
            var raw = new AdmxPolicy
            {
                RegistryKey = "Software\\PolicyPlusTest",
                RegistryValue = "RootVal",
                Section = AdmxPolicySection.Machine,
                AffectedValues = new PolicyRegistryList
                {
                    OnValue = new PolicyRegistryValue
                    {
                        RegistryType = PolicyRegistryValueType.Numeric,
                        NumberValue = 1U,
                    },
                },
                Elements = new List<PolicyElement> { boolElem },
                DefinedIn = Dummy(),
            };
            var pol = new PolicyPlusPolicy
            {
                UniqueID = "M:Weight",
                DisplayName = "Weight",
                RawPolicy = raw,
            };
            var src = new InMem();
            src.SetValue(
                raw.RegistryKey,
                raw.RegistryValue,
                1U,
                Microsoft.Win32.RegistryValueKind.DWord
            );
            src.SetValue(
                "Software\\PolicyPlusTest",
                "Flag",
                0U,
                Microsoft.Win32.RegistryValueKind.DWord
            );
            var state = global::PolicyPlusCore.Core.PolicyProcessing.GetPolicyState(src, pol);
            Assert.Equal(PolicyState.Enabled, state);
        }

        [Fact(DisplayName = "GetReferencedRegistryValues de-duplicates overlaps")]
        public void GetReferencedRegistryValues_Dedupes()
        {
            var enumElem = new EnumPolicyElement
            {
                ID = "E",
                ElementType = "enum",
                RegistryKey = "Software\\PolicyPlusTest",
                RegistryValue = "Combo",
                Items = new List<EnumPolicyElementItem>
                {
                    new EnumPolicyElementItem
                    {
                        Value = new PolicyRegistryValue
                        {
                            RegistryType = PolicyRegistryValueType.Numeric,
                            NumberValue = 1U,
                        },
                        DisplayCode = "One",
                        ValueList = new PolicyRegistrySingleList
                        {
                            AffectedValues = new List<PolicyRegistryListEntry>
                            {
                                new PolicyRegistryListEntry
                                {
                                    RegistryValue = "Aux",
                                    Value = new PolicyRegistryValue
                                    {
                                        RegistryType = PolicyRegistryValueType.Numeric,
                                        NumberValue = 2U,
                                    },
                                },
                            },
                        },
                    },
                },
            };
            var raw = new AdmxPolicy
            {
                RegistryKey = "Software\\PolicyPlusTest",
                RegistryValue = "Aux",
                Section = AdmxPolicySection.Machine,
                Elements = new List<PolicyElement> { enumElem },
                AffectedValues = new PolicyRegistryList(),
                DefinedIn = Dummy(),
            };
            var pol = new PolicyPlusPolicy
            {
                UniqueID = "M:Ref",
                DisplayName = "Ref",
                RawPolicy = raw,
            };
            var refs = global::PolicyPlusCore.Core.PolicyProcessing.GetReferencedRegistryValues(
                pol
            );
            // Expect only two unique pairs: root (Key, Aux) and enum main (Key, Combo)
            Assert.Equal(2, refs.Count);
        }

        [Fact(DisplayName = "ForgetPolicy clears enum ValueList entries")]
        public void ForgetPolicy_EnumValueList_Clears()
        {
            var enumElem = new EnumPolicyElement
            {
                ID = "E",
                ElementType = "enum",
                RegistryKey = "Software\\PolicyPlusTest",
                RegistryValue = "EnumMain",
                Items = new List<EnumPolicyElementItem>
                {
                    new EnumPolicyElementItem
                    {
                        Value = new PolicyRegistryValue
                        {
                            RegistryType = PolicyRegistryValueType.Numeric,
                            NumberValue = 1U,
                        },
                        DisplayCode = "One",
                        ValueList = new PolicyRegistrySingleList
                        {
                            AffectedValues = new List<PolicyRegistryListEntry>
                            {
                                new PolicyRegistryListEntry
                                {
                                    RegistryValue = "Aux1",
                                    Value = new PolicyRegistryValue
                                    {
                                        RegistryType = PolicyRegistryValueType.Numeric,
                                        NumberValue = 10U,
                                    },
                                },
                            },
                        },
                    },
                },
            };
            var raw = new AdmxPolicy
            {
                RegistryKey = "Software\\PolicyPlusTest",
                RegistryValue = "EnumMain",
                Section = AdmxPolicySection.Machine,
                Elements = new List<PolicyElement> { enumElem },
                AffectedValues = new PolicyRegistryList(),
                DefinedIn = Dummy(),
            };
            var pol = new PolicyPlusPolicy
            {
                UniqueID = "M:ForgetEnum",
                DisplayName = "ForgetEnum",
                RawPolicy = raw,
            };
            var src = new InMem();
            global::PolicyPlusCore.Core.PolicyProcessing.SetPolicyState(
                src,
                pol,
                PolicyState.Enabled,
                new Dictionary<string, object> { { "E", 0 } }
            );
            Assert.True(src.ContainsValue(raw.RegistryKey, "Aux1"));
            global::PolicyPlusCore.Core.PolicyProcessing.ForgetPolicy(src, pol);
            Assert.False(src.ContainsValue(raw.RegistryKey, "Aux1"));
        }

        [Fact(DisplayName = "IsPolicySupported cycle does not infinite loop and returns false")]
        public void IsPolicySupported_Cycle_ReturnsFalse()
        {
            var prod = new PolicyPlusProduct
            {
                UniqueID = "P",
                DisplayName = "P",
                RawProduct = new AdmxProduct
                {
                    ID = "P",
                    Version = 0,
                    DefinedIn = Dummy(),
                },
            };
            var supAraw = new AdmxSupportDefinition
            {
                ID = "A",
                Logic = AdmxSupportLogicType.AnyOf,
                DefinedIn = Dummy(),
            };
            var supBraw = new AdmxSupportDefinition
            {
                ID = "B",
                Logic = AdmxSupportLogicType.AnyOf,
                DefinedIn = Dummy(),
            };
            var supA = new PolicyPlusSupport
            {
                UniqueID = "A",
                DisplayName = "A",
                RawSupport = supAraw,
            };
            var supB = new PolicyPlusSupport
            {
                UniqueID = "B",
                DisplayName = "B",
                RawSupport = supBraw,
            };
            supA.Elements.Add(
                new PolicyPlusSupportEntry
                {
                    SupportDefinition = supB,
                    RawSupportEntry = new AdmxSupportEntry { ProductID = "B" },
                }
            );
            supB.Elements.Add(
                new PolicyPlusSupportEntry
                {
                    SupportDefinition = supA,
                    RawSupportEntry = new AdmxSupportEntry { ProductID = "A" },
                }
            );
            var raw = new AdmxPolicy
            {
                RegistryKey = "Software\\PolicyPlusTest",
                RegistryValue = "Cyc",
                Section = AdmxPolicySection.Machine,
                AffectedValues = new PolicyRegistryList(),
                Elements = new List<PolicyElement>(),
                DefinedIn = Dummy(),
            };
            var pol = new PolicyPlusPolicy
            {
                UniqueID = "M:Cyc",
                DisplayName = "Cyc",
                RawPolicy = raw,
                SupportedOn = supA,
            };
            bool supported = global::PolicyPlusCore.Core.PolicyProcessing.IsPolicySupported(
                pol,
                new List<PolicyPlusProduct> { prod },
                false,
                false
            );
            Assert.False(supported);
        }
    }
}
