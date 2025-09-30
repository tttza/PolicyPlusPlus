using System.Collections.Generic;
using PolicyPlusCore.Admx;
using PolicyPlusCore.Core;
using PolicyPlusModTests.Testing;
using Xunit;

namespace PolicyPlusModTests.Core.PolicyProcessingAdvanced
{
    public class PolicyProcessingStateEvaluationAdvancedTests
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

        [Fact(DisplayName = "Enum item hit -> explicit Enabled")]
        public void EnumItemHit_ExplicitEnabled()
        {
            var pol = TestPolicyFactory.CreateEnumPolicy("MACHINE:EnumExplicit");
            var file = new PolFile();
            // Set item 1 (NumberValue=1U)
            file.SetValue(
                pol.RawPolicy.RegistryKey,
                pol.RawPolicy.RegistryValue,
                1U,
                Microsoft.Win32.RegistryValueKind.DWord
            );
            var src = new InMemorySource(file);
            var state = PolicyPlusCore.Core.PolicyProcessing.GetPolicyState(src, pol);
            Assert.Equal(PolicyState.Enabled, state);
        }

        [Fact(DisplayName = "Synthetic (no root On/Off) boolean OnValue only -> Enabled")]
        public void SyntheticElementOnly_Enabled()
        {
            var pol = TestPolicyFactory.CreateSimpleTogglePolicy("MACHINE:SynthOn");
            // Root AffectedValues has no On/Off configured
            var boolElem = new BooleanPolicyElement
            {
                ID = "B",
                ElementType = "boolean",
                RegistryKey = pol.RawPolicy.RegistryKey,
                RegistryValue = "Flag",
                AffectedRegistry = new PolicyRegistryList
                {
                    OnValue = new PolicyRegistryValue
                    {
                        RegistryType = PolicyRegistryValueType.Numeric,
                        NumberValue = 1U,
                    },
                },
            };
            pol.RawPolicy.Elements = new List<PolicyElement> { boolElem };
            var file = new PolFile();
            file.SetValue(
                pol.RawPolicy.RegistryKey,
                "Flag",
                1U,
                Microsoft.Win32.RegistryValueKind.DWord
            );
            var src = new InMemorySource(file);
            var state = PolicyPlusCore.Core.PolicyProcessing.GetPolicyState(src, pol);
            Assert.Equal(PolicyState.Enabled, state);
        }

        [Fact(DisplayName = "Synthetic (no root On/Off) element deletion only -> Disabled")]
        public void SyntheticDeletionOnly_Disabled()
        {
            var pol = TestPolicyFactory.CreateSimpleTogglePolicy("MACHINE:SynthDel");
            var boolElem = new BooleanPolicyElement
            {
                ID = "B",
                ElementType = "boolean",
                RegistryKey = pol.RawPolicy.RegistryKey,
                RegistryValue = "FlagDel",
            };
            pol.RawPolicy.Elements = new List<PolicyElement> { boolElem };
            var file = new PolFile();
            file.SetValue(
                pol.RawPolicy.RegistryKey,
                "FlagDel",
                0U,
                Microsoft.Win32.RegistryValueKind.DWord
            );
            file.DeleteValue(pol.RawPolicy.RegistryKey, "FlagDel"); // deletion evidence
            var src = new InMemorySource(file);
            var state = PolicyPlusCore.Core.PolicyProcessing.GetPolicyState(src, pol);
            Assert.Equal(PolicyState.Disabled, state);
        }

        [Fact(DisplayName = "Hex numeric equivalence (NumberValue=0x2, stored=2) -> Enabled")]
        public void HexNumericMatch_Enabled()
        {
            var pol = TestPolicyFactory.CreateSimpleTogglePolicy("MACHINE:HexNum");
            pol.RawPolicy.AffectedValues.OnValue = new PolicyRegistryValue
            {
                RegistryType = PolicyRegistryValueType.Numeric,
                NumberValue = 0x2U,
            };
            var file = new PolFile();
            file.SetValue(
                pol.RawPolicy.RegistryKey,
                pol.RawPolicy.RegistryValue,
                2U,
                Microsoft.Win32.RegistryValueKind.DWord
            );
            var src = new InMemorySource(file);
            var state = PolicyPlusCore.Core.PolicyProcessing.GetPolicyState(src, pol);
            Assert.Equal(PolicyState.Enabled, state);
        }

        [Fact(DisplayName = "Text OnValue mismatch -> NotConfigured (evidence 0)")]
        public void TextMismatch_NotConfigured()
        {
            var pol = TestPolicyFactory.CreateSimpleTogglePolicy("MACHINE:TxtMismatch");
            pol.RawPolicy.AffectedValues.OnValue = new PolicyRegistryValue
            {
                RegistryType = PolicyRegistryValueType.Text,
                StringValue = "ABC",
            };
            // No OffValue: synthetic deletion not flagged so disabledEvidence stays 0
            var file = new PolFile();
            file.SetValue(
                pol.RawPolicy.RegistryKey,
                pol.RawPolicy.RegistryValue,
                "ABD",
                Microsoft.Win32.RegistryValueKind.String
            );
            var src = new InMemorySource(file);
            var state = PolicyPlusCore.Core.PolicyProcessing.GetPolicyState(src, pol);
            Assert.Equal(PolicyState.NotConfigured, state);
        }
    }
}
