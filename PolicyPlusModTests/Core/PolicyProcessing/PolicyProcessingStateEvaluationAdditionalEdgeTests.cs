using System.Collections.Generic;
using PolicyPlusCore.Admx;
using PolicyPlusCore.Core;
using PolicyPlusModTests.Testing;
using Xunit;

namespace PolicyPlusModTests.Core.PolicyProcessingEdge
{
    public class PolicyProcessingStateEvaluationAdditionalEdgeTests
    {
        private sealed class InMemorySource : IPolicySource
        {
            private readonly PolFile _pol;

            public InMemorySource(PolFile p)
            {
                _pol = p;
            }

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
        }

        [Fact(DisplayName = "Enum item + ValueList all match -> explicit Enabled")]
        public void EnumItemValueList_ExplicitEnabled()
        {
            var pol = TestPolicyFactory.CreateEnumPolicy("MACHINE:EnumWithValueList");
            var enumElem = (EnumPolicyElement)pol.RawPolicy.Elements[0];
            // Add ValueList to first item (Value=1U plus two auxiliary values)
            enumElem.Items[0].ValueList = new PolicyRegistrySingleList
            {
                AffectedValues = new List<PolicyRegistryListEntry>
                {
                    new PolicyRegistryListEntry
                    {
                        RegistryValue = "AuxA",
                        Value = new PolicyRegistryValue
                        {
                            RegistryType = PolicyRegistryValueType.Numeric,
                            NumberValue = 10U,
                        },
                    },
                    new PolicyRegistryListEntry
                    {
                        RegistryValue = "AuxB",
                        Value = new PolicyRegistryValue
                        {
                            RegistryType = PolicyRegistryValueType.Numeric,
                            NumberValue = 11U,
                        },
                    },
                },
            };
            var file = new PolFile();
            // Enum main value
            file.SetValue(
                pol.RawPolicy.RegistryKey,
                pol.RawPolicy.RegistryValue,
                1U,
                Microsoft.Win32.RegistryValueKind.DWord
            );
            // Add all auxiliary list values
            file.SetValue(
                pol.RawPolicy.RegistryKey,
                "AuxA",
                10U,
                Microsoft.Win32.RegistryValueKind.DWord
            );
            file.SetValue(
                pol.RawPolicy.RegistryKey,
                "AuxB",
                11U,
                Microsoft.Win32.RegistryValueKind.DWord
            );
            var src = new InMemorySource(file);
            var state = PolicyPlusCore.Core.PolicyProcessing.GetPolicyState(src, pol);
            Assert.Equal(PolicyState.Enabled, state);
        }

        [Fact(
            DisplayName = "Boolean On/Off both explicit; evidence tie -> Unknown (explicit conflict fallback)"
        )]
        public void BooleanExplicitConflict_EvidenceTie_Unknown()
        {
            var pol = TestPolicyFactory.CreateSimpleTogglePolicy("MACHINE:BoolConflict");
            // Use single-entry OnValueList and OffValueList to produce explicitPos and explicitNeg
            pol.RawPolicy.AffectedValues.OnValueList = new PolicyRegistrySingleList
            {
                AffectedValues = new List<PolicyRegistryListEntry>
                {
                    new PolicyRegistryListEntry
                    {
                        RegistryValue = "OnE",
                        Value = new PolicyRegistryValue
                        {
                            RegistryType = PolicyRegistryValueType.Numeric,
                            NumberValue = 5U,
                        },
                    },
                },
            };
            pol.RawPolicy.AffectedValues.OffValueList = new PolicyRegistrySingleList
            {
                AffectedValues = new List<PolicyRegistryListEntry>
                {
                    new PolicyRegistryListEntry
                    {
                        RegistryValue = "OffE",
                        Value = new PolicyRegistryValue
                        {
                            RegistryType = PolicyRegistryValueType.Numeric,
                            NumberValue = 6U,
                        },
                    },
                },
            };
            var file = new PolFile();
            file.SetValue(
                pol.RawPolicy.RegistryKey,
                "OnE",
                5U,
                Microsoft.Win32.RegistryValueKind.DWord
            );
            file.SetValue(
                pol.RawPolicy.RegistryKey,
                "OffE",
                6U,
                Microsoft.Win32.RegistryValueKind.DWord
            );
            var src = new InMemorySource(file);
            var state = PolicyPlusCore.Core.PolicyProcessing.GetPolicyState(src, pol);
            Assert.Equal(PolicyState.Unknown, state);
        }

        [Fact(
            DisplayName = "MultiText present vs NamedList deleted (MultiText=Enabled, NamedList=Disabled)"
        )]
        public void MultiTextAndNamedList_SplitState()
        {
            // MultiText policy with evidence
            var multi = TestPolicyFactory.CreateMultiTextPolicy("MACHINE:MTForSplit");
            var file = new PolFile();
            file.SetValue(
                multi.RawPolicy.RegistryKey,
                multi.RawPolicy.RegistryValue,
                new[] { "L1", "L2" },
                Microsoft.Win32.RegistryValueKind.MultiString
            );
            var multiSrc = new InMemorySource(file);
            var multiState = PolicyPlusCore.Core.PolicyProcessing.GetPolicyState(multiSrc, multi);
            Assert.Equal(PolicyState.Enabled, multiState);

            // NamedList policy deletion evidence: create entry then ClearKey
            var named = TestPolicyFactory.CreateNamedListPolicy("MACHINE:NamedListForSplit");
            file.SetValue(
                named.RawPolicy.RegistryKey,
                "Entry1",
                "A",
                Microsoft.Win32.RegistryValueKind.String
            );
            file.ClearKey(named.RawPolicy.RegistryKey);
            var namedSrc = new InMemorySource(file);
            var namedState = PolicyPlusCore.Core.PolicyProcessing.GetPolicyState(namedSrc, named);
            Assert.Equal(PolicyState.Disabled, namedState);
        }
    }
}
