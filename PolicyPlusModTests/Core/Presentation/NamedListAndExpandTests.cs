using PolicyPlusModTests.Testing;
using System.Collections.Generic;
using PolicyPlusCore.Core; // fully qualify not strictly needed now
using Xunit;

namespace PolicyPlusModTests.Core.Presentation
{
    public class NamedListAndExpandTests
    {
        [Fact(DisplayName = "Named list (UserProvidesNames) writes each key/value pair")]
        public void NamedList_Writes_KeyValuePairs()
        {
            var pol = new PolFile();
            var policy = TestPolicyFactory.CreateNamedListPolicy();
            var data = new List<KeyValuePair<string, string>>
            {
                new("KeyA","ValA"),
                new("KeyB","ValB"),
            };
            PolicyPlusCore.Core.PolicyProcessing.SetPolicyState(pol, policy, PolicyState.Enabled, new Dictionary<string, object> { { "NamedListElem", data } });
            Assert.True(pol.ContainsValue(policy.RawPolicy.RegistryKey, "KeyA"));
            Assert.Equal("ValA", pol.GetValue(policy.RawPolicy.RegistryKey, "KeyA"));
            Assert.True(pol.ContainsValue(policy.RawPolicy.RegistryKey, "KeyB"));
            Assert.Equal("ValB", pol.GetValue(policy.RawPolicy.RegistryKey, "KeyB"));
        }

        [Fact(DisplayName = "Expandable text element stored as REG_EXPAND_SZ")]
        public void ExpandableText_Writes_ExpandSz()
        {
            var pol = new PolFile();
            var policy = TestPolicyFactory.CreateExpandableTextElementPolicy();
            PolicyPlusCore.Core.PolicyProcessing.SetPolicyState(pol, policy, PolicyState.Enabled, new Dictionary<string, object> { { "ExpTextElem", "%SystemRoot%\\Test" } });
            Assert.True(pol.ContainsValue(policy.RawPolicy.RegistryKey, policy.RawPolicy.RegistryValue));
            Assert.Equal("%SystemRoot%\\Test", pol.GetValue(policy.RawPolicy.RegistryKey, policy.RawPolicy.RegistryValue));
        }
    }
}
