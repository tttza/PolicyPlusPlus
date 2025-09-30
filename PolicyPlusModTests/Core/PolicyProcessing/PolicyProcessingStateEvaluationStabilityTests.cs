using System.Collections.Generic;
using PolicyPlusCore.Admx;
using PolicyPlusCore.Core;
using PolicyPlusModTests.Testing;
using Xunit;

namespace PolicyPlusModTests.Core.PolicyProcessingStability
{
    public class PolicyProcessingStateEvaluationStabilityTests
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

        [Fact(DisplayName = "CachedPolicySource consecutive calls are idempotent")]
        public void CachedPolicySource_Idempotent()
        {
            var pol = TestPolicyFactory.CreateListPolicy("MACHINE:CacheIdem");
            var file = new PolFile();
            file.SetValue(
                "Software\\PolicyPlusTest",
                "ListPrefix1",
                "A",
                Microsoft.Win32.RegistryValueKind.String
            );
            var src = new InMemorySource(file);
            var first = PolicyPlusCore.Core.PolicyProcessing.GetPolicyState(src, pol);
            var second = PolicyPlusCore.Core.PolicyProcessing.GetPolicyState(src, pol);
            Assert.Equal(first, second);
            Assert.Equal(PolicyState.Enabled, first);
        }

        [Fact(
            DisplayName = "Explicit root OnValue precedence over evidence (MixedElements_PriorityOrder)"
        )]
        public void MixedElements_PriorityOrder()
        {
            var pol = TestPolicyFactory.CreateListPolicy("MACHINE:MixedPriority");
            // Add root OnValue so explicitPos triggers
            pol.RawPolicy.AffectedValues.OnValue = new PolicyRegistryValue
            {
                RegistryType = PolicyRegistryValueType.Numeric,
                NumberValue = 99U,
            };
            // Create list-side deletion evidence (clear key) yet explicit should win
            var file = new PolFile();
            file.SetValue(
                pol.RawPolicy.RegistryKey,
                pol.RawPolicy.RegistryValue,
                99U,
                Microsoft.Win32.RegistryValueKind.DWord
            );
            file.SetValue(
                "Software\\PolicyPlusTest",
                "ListPrefix1",
                "X",
                Microsoft.Win32.RegistryValueKind.String
            );
            file.ClearKey("Software\\PolicyPlusTest"); // Build disabledEvidence via list element deletion
            // Root value removed by ClearKey, re-add to restore explicitPos
            file.SetValue(
                pol.RawPolicy.RegistryKey,
                pol.RawPolicy.RegistryValue,
                99U,
                Microsoft.Win32.RegistryValueKind.DWord
            );
            var src = new InMemorySource(file);
            var state = PolicyPlusCore.Core.PolicyProcessing.GetPolicyState(src, pol);
            Assert.Equal(PolicyState.Enabled, state);
        }
    }
}
