using System.Collections.Generic;
using PolicyPlusCore.Admx; // ADMX model types
using PolicyPlusCore.Core;
using PolicyPlusModTests.Testing;
using Xunit;

namespace PolicyPlusModTests.Core.PolicyProcessingStates
{
    public class PolicyProcessingStateEvaluationTests
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

        [Fact(DisplayName = "Root OnValue alone -> Enabled (explicitPos)")]
        public void RootOnValue_Enables()
        {
            var pol = TestPolicyFactory.CreateSimpleTogglePolicy("MACHINE:RootOn");
            pol.RawPolicy.AffectedValues.OnValue = new PolicyRegistryValue
            {
                RegistryType = PolicyRegistryValueType.Numeric,
                NumberValue = 1U,
            };
            var file = new PolFile();
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

        [Fact(DisplayName = "Root OffValue(Delete) alone -> Disabled (explicitNeg)")]
        public void RootOffValue_Disables()
        {
            var pol = TestPolicyFactory.CreateSimpleTogglePolicy("MACHINE:RootOff");
            pol.RawPolicy.AffectedValues.OffValue = new PolicyRegistryValue
            {
                RegistryType = PolicyRegistryValueType.Delete,
            };
            var file = new PolFile();
            // Need existing value then delete so WillDeleteValue reports true
            file.SetValue(
                pol.RawPolicy.RegistryKey,
                pol.RawPolicy.RegistryValue,
                123U,
                Microsoft.Win32.RegistryValueKind.DWord
            );
            file.DeleteValue(pol.RawPolicy.RegistryKey, pol.RawPolicy.RegistryValue);
            var src = new InMemorySource(file);
            var state = PolicyPlusCore.Core.PolicyProcessing.GetPolicyState(src, pol);
            Assert.Equal(PolicyState.Disabled, state);
        }

        [Fact(DisplayName = "Boolean OffValue only match -> Disabled")]
        public void BooleanOffOnly_Disables()
        {
            var pol = TestPolicyFactory.CreateSimpleTogglePolicy("MACHINE:BoolOffOnly");
            var boolElem = new BooleanPolicyElement
            {
                ID = "Bool",
                ElementType = "boolean",
                RegistryKey = pol.RawPolicy.RegistryKey,
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
            pol.RawPolicy.Elements = new List<PolicyElement> { boolElem };
            var file = new PolFile();
            file.SetValue(
                pol.RawPolicy.RegistryKey,
                "Flag",
                0U,
                Microsoft.Win32.RegistryValueKind.DWord
            );
            var src = new InMemorySource(file);
            var state = PolicyPlusCore.Core.PolicyProcessing.GetPolicyState(src, pol);
            Assert.Equal(PolicyState.Disabled, state);
        }

        [Fact(DisplayName = "List element values present -> Enabled (presentElements > 0)")]
        public void ListPresent_Enables()
        {
            var pol = TestPolicyFactory.CreateListPolicy("MACHINE:ListPresent");
            var file = new PolFile();
            file.SetValue(
                "Software\\PolicyPlusTest",
                "ListPrefix1",
                "A",
                Microsoft.Win32.RegistryValueKind.String
            );
            file.SetValue(
                "Software\\PolicyPlusTest",
                "ListPrefix2",
                "B",
                Microsoft.Win32.RegistryValueKind.String
            );
            var src = new InMemorySource(file);
            var state = PolicyPlusCore.Core.PolicyProcessing.GetPolicyState(src, pol);
            Assert.Equal(PolicyState.Enabled, state);
        }

        [Fact(DisplayName = "List element deletion -> Disabled (deletedElements > 0)")]
        public void ListDeleted_Disables()
        {
            var pol = TestPolicyFactory.CreateListPolicy("MACHINE:ListDeleted");
            var file = new PolFile();
            // Write value then ClearKey to simulate deletion
            file.SetValue(
                "Software\\PolicyPlusTest",
                "ListPrefix1",
                "A",
                Microsoft.Win32.RegistryValueKind.String
            );
            file.ClearKey("Software\\PolicyPlusTest");
            var src = new InMemorySource(file);
            var state = PolicyPlusCore.Core.PolicyProcessing.GetPolicyState(src, pol);
            Assert.Equal(PolicyState.Disabled, state);
        }

        [Fact(DisplayName = "Enabled/Disabled evidence tie (non-zero) -> Unknown")]
        public void EvidenceTie_Unknown()
        {
            var pol = TestPolicyFactory.CreateSimpleTogglePolicy("MACHINE:TieUnknown");
            // Use one OnValueList and one OffValueList entry to produce 1 vs 1 evidence
            pol.RawPolicy.AffectedValues.OnValueList = new PolicyRegistrySingleList
            {
                AffectedValues = new List<PolicyRegistryListEntry>
                {
                    new PolicyRegistryListEntry
                    {
                        RegistryValue = "OnVal",
                        Value = new PolicyRegistryValue
                        {
                            RegistryType = PolicyRegistryValueType.Numeric,
                            NumberValue = 10U,
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
                        RegistryValue = "OffVal",
                        Value = new PolicyRegistryValue
                        {
                            RegistryType = PolicyRegistryValueType.Numeric,
                            NumberValue = 20U,
                        },
                    },
                },
            };
            var file = new PolFile();
            file.SetValue(
                pol.RawPolicy.RegistryKey,
                "OnVal",
                10U,
                Microsoft.Win32.RegistryValueKind.DWord
            );
            file.SetValue(
                pol.RawPolicy.RegistryKey,
                "OffVal",
                20U,
                Microsoft.Win32.RegistryValueKind.DWord
            );
            var src = new InMemorySource(file);
            var state = PolicyPlusCore.Core.PolicyProcessing.GetPolicyState(src, pol);
            Assert.Equal(PolicyState.Unknown, state);
        }
    }
}
