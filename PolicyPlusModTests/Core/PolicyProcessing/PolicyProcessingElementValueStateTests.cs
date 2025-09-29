using System.Collections.Generic;
using PolicyPlusCore.Core;
using PolicyPlusCore.IO;
using PolicyPlusModTests.Testing;
using Xunit;

namespace PolicyPlusModTests.Core.PolicyProcessing
{
    public class PolicyProcessingElementValueStateTests
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

        [Fact(DisplayName = "Decimal element registry value alone enables policy")]
        public void DecimalElementEnables()
        {
            var polDef = TestPolicyFactory.CreateDecimalPolicy("MACHINE:DecStateTest");
            var polFile = new PolFile();
            polFile.SetValue(
                "Software\\PolicyPlusTest",
                "DecimalValue",
                42U,
                Microsoft.Win32.RegistryValueKind.DWord
            );
            var src = new InMemorySource(polFile);
            var state = global::PolicyPlusCore.Core.PolicyProcessing.GetPolicyState(src, polDef);
            Assert.Equal(PolicyState.Enabled, state);
        }

        [Fact(DisplayName = "MultiText element registry value alone enables policy")]
        public void MultiTextElementEnables()
        {
            var polDef = TestPolicyFactory.CreateMultiTextPolicy("MACHINE:MultiTxtStateTest");
            var polFile = new PolFile();
            polFile.SetValue(
                "Software\\PolicyPlusTest",
                "MultiTextValue",
                new[] { "A", "B" },
                Microsoft.Win32.RegistryValueKind.MultiString
            );
            var src = new InMemorySource(polFile);
            var state = global::PolicyPlusCore.Core.PolicyProcessing.GetPolicyState(src, polDef);
            Assert.Equal(PolicyState.Enabled, state);
        }

        [Fact(DisplayName = "Decimal element absent => NotConfigured")]
        public void DecimalElementNotConfigured()
        {
            var polDef = TestPolicyFactory.CreateDecimalPolicy("MACHINE:DecStateNone");
            var polFile = new PolFile();
            var src = new InMemorySource(polFile);
            var state = global::PolicyPlusCore.Core.PolicyProcessing.GetPolicyState(src, polDef);
            Assert.Equal(PolicyState.NotConfigured, state);
        }
    }
}
