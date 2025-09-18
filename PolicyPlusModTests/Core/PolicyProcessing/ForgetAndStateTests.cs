using System.Collections.Generic;
using PolicyPlusCore.Core; // core models
using PolicyPlusCore.Admx; // ADMX models
using Xunit;

namespace PolicyPlusModTests.Core.PolicyProcessingSpecs
{
    public class ForgetAndStateTests
    {
        [Fact(DisplayName = "ForgetPolicy clears list keys and forgets values")]
        public void ForgetPolicy_Clears_List()
        {
            var polFile = new PolFile();
            var listElem = new ListPolicyElement
            {
                ID = "ListElem",
                ElementType = "list",
                RegistryKey = "Software\\PolicyPlusTest",
                RegistryValue = "Prefix",
                HasPrefix = true
            };
            var raw = new AdmxPolicy
            {
                RegistryKey = "Software\\PolicyPlusTest",
                RegistryValue = "Prefix",
                Section = AdmxPolicySection.Machine,
                Elements = new List<PolicyElement> { listElem },
                AffectedValues = new PolicyRegistryList(),
                DefinedIn = new AdmxFile { SourceFile = "dummy.admx" }
            };
            var policy = new PolicyPlusPolicy { RawPolicy = raw, UniqueID = "MACHINE:List", DisplayName = "List" };

            global::PolicyPlusCore.Core.PolicyProcessing.SetPolicyState(polFile, policy, PolicyState.Enabled, new Dictionary<string, object> { { "ListElem", new List<string> { "A", "B" } } });
            Assert.True(polFile.GetValueNames("Software\\PolicyPlusTest").Count > 0);

            global::PolicyPlusCore.Core.PolicyProcessing.ForgetPolicy(polFile, policy);
            Assert.Empty(polFile.GetValueNames("Software\\PolicyPlusTest"));
        }

        [Fact(DisplayName = "GetPolicyState returns Enabled when boolean OnValue present")]
        public void GetPolicyState_Boolean_OnValue_Present()
        {
            var polFile = new PolFile();
            var boolElem = new BooleanPolicyElement
            {
                ID = "Bool",
                ElementType = "boolean",
                RegistryKey = "Software\\PolicyPlusTest",
                RegistryValue = "Flag",
                AffectedRegistry = new PolicyRegistryList
                {
                    OnValue = new PolicyRegistryValue { RegistryType = PolicyRegistryValueType.Numeric, NumberValue = 1U },
                    OffValue = new PolicyRegistryValue { RegistryType = PolicyRegistryValueType.Delete }
                }
            };
            var raw = new AdmxPolicy
            {
                RegistryKey = "Software\\PolicyPlusTest",
                RegistryValue = "Flag",
                Section = AdmxPolicySection.Machine,
                Elements = new List<PolicyElement> { boolElem },
                AffectedValues = new PolicyRegistryList(),
                DefinedIn = new AdmxFile { SourceFile = "dummy.admx" }
            };
            var policy = new PolicyPlusPolicy { RawPolicy = raw, UniqueID = "MACHINE:Bool", DisplayName = "Bool" };

            polFile.SetValue(raw.RegistryKey, raw.RegistryValue, 1U, Microsoft.Win32.RegistryValueKind.DWord);
            var state = global::PolicyPlusCore.Core.PolicyProcessing.GetPolicyState(polFile, policy);
            Assert.Equal(PolicyState.Enabled, state);
        }
    }
}
