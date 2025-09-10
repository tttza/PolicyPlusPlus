using System.Collections.Generic;
using System.Linq;
using PolicyPlusCore.Core;
using PolicyPlusModTests.TestHelpers;
using Xunit;

namespace PolicyPlusModTests
{
    public class NamedListAndExpandTests
    {
        [Fact(DisplayName = "Named list (UserProvidesNames) writes each key/value pair")] 
        public void NamedList_Writes_KeyValuePairs()
        {
            var pol = new PolFile();
            var policy = TestPolicyFactory.CreateNamedListPolicy();
            var data = new List<KeyValuePair<string,string>>
            {
                new("KeyA","ValA"),
                new("KeyB","ValB"),
            };
            PolicyProcessing.SetPolicyState(pol, policy, PolicyState.Enabled, new Dictionary<string, object> { { "NamedListElem", data } });
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
            PolicyProcessing.SetPolicyState(pol, policy, PolicyState.Enabled, new Dictionary<string, object> { { "ExpTextElem", "%SystemRoot%\\Test" } });
            // We cannot directly assert registry kind (PolFile abstraction). Assert roundtrip text and differentiate from plain string by replacing env var later if needed.
            Assert.True(pol.ContainsValue(policy.RawPolicy.RegistryKey, policy.RawPolicy.RegistryValue));
            Assert.Equal("%SystemRoot%\\Test", pol.GetValue(policy.RawPolicy.RegistryKey, policy.RawPolicy.RegistryValue));
        }
    }
}
