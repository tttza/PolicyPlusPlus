using PolicyPlusModTests.Testing;
using PolicyPlusCore.Core; // core models
using System.Collections.Generic;
using Xunit;

namespace PolicyPlusModTests.Core.Presentation
{
    public class AdditionalPresentationValidationTests
    {
        [Fact(DisplayName = "ComboBox defaultText is written when enabled without user edit")]
        public void ComboBox_DefaultText_Applied()
        {
            var pol = new PolFile();
            var policy = TestPolicyFactory.CreateComboBoxTextPolicy();
            global::PolicyPlusCore.Core.PolicyProcessing.SetPolicyState(pol, policy, PolicyState.Enabled, new Dictionary<string, object> { { "ComboTextElem", "DefaultCombo" } });
            Assert.True(pol.ContainsValue(policy.RawPolicy.RegistryKey, policy.RawPolicy.RegistryValue));
            Assert.Equal("DefaultCombo", pol.GetValue(policy.RawPolicy.RegistryKey, policy.RawPolicy.RegistryValue));
        }

        [Fact(DisplayName = "Text MaxLength enforced by truncation")]
        public void Text_MaxLength_Truncates()
        {
            var pol = new PolFile();
            var policy = TestPolicyFactory.CreateMaxLengthTextPolicy(maxLen: 5);
            global::PolicyPlusCore.Core.PolicyProcessing.SetPolicyState(pol, policy, PolicyState.Enabled, new Dictionary<string, object> { { "MaxLenTextElem", "123456789" } });
            Assert.Equal("12345", pol.GetValue(policy.RawPolicy.RegistryKey, policy.RawPolicy.RegistryValue));
        }

        [Fact(DisplayName = "Enum non-sequential values mapping persists actual numeric value")]
        public void Enum_NonSequential_Writes_ActualValue()
        {
            var pol = new PolFile();
            var enumElem = new EnumPolicyElement
            {
                ID = "EnumNS",
                ElementType = "enum",
                RegistryKey = "Software\\PolicyPlusTest",
                RegistryValue = "EnumNSValue",
                Items = new List<EnumPolicyElementItem>
                {
                    new() { Value = new PolicyRegistryValue{ RegistryType = PolicyRegistryValueType.Numeric, NumberValue = 10}, DisplayCode = "Ten" },
                    new() { Value = new PolicyRegistryValue{ RegistryType = PolicyRegistryValueType.Numeric, NumberValue = 20}, DisplayCode = "Twenty" },
                    new() { Value = new PolicyRegistryValue{ RegistryType = PolicyRegistryValueType.Numeric, NumberValue = 30}, DisplayCode = "Thirty" },
                }
            };
            var raw = new AdmxPolicy { RegistryKey = enumElem.RegistryKey, RegistryValue = enumElem.RegistryValue, Section = AdmxPolicySection.Machine, Elements = new List<PolicyElement> { enumElem }, AffectedValues = new PolicyRegistryList(), DefinedIn = new AdmxFile { SourceFile = "dummy.admx" } };
            var policy = new PolicyPlusPolicy { RawPolicy = raw, UniqueID = "MACHINE:EnumNonSeq2", DisplayName = "Enum NonSeq2" };
            global::PolicyPlusCore.Core.PolicyProcessing.SetPolicyState(pol, policy, PolicyState.Enabled, new Dictionary<string, object> { { "EnumNS", 1 } });
            Assert.Equal(20u, pol.GetValue(enumElem.RegistryKey, enumElem.RegistryValue));
        }

        [Fact(DisplayName = "Decimal spinner increment placeholder retains provided value")]
        public void Decimal_Spinner_Increment_Placeholder()
        {
            var pol = new PolFile();
            var decElem = new DecimalPolicyElement { ID = "DecSpin", ElementType = "decimal", RegistryKey = "Software\\PolicyPlusTest", RegistryValue = "DecSpinValue", Minimum = 0, Maximum = 100, StoreAsText = false };
            var raw = new AdmxPolicy { RegistryKey = decElem.RegistryKey, RegistryValue = decElem.RegistryValue, Section = AdmxPolicySection.Machine, Elements = new List<PolicyElement> { decElem }, AffectedValues = new PolicyRegistryList(), DefinedIn = new AdmxFile { SourceFile = "dummy.admx" } };
            var policy = new PolicyPlusPolicy { RawPolicy = raw, UniqueID = "MACHINE:DecSpin", DisplayName = "DecSpin" };
            global::PolicyPlusCore.Core.PolicyProcessing.SetPolicyState(pol, policy, PolicyState.Enabled, new Dictionary<string, object> { { "DecSpin", 7u } });
            Assert.Equal(7u, pol.GetValue(decElem.RegistryKey, decElem.RegistryValue));
        }
    }
}
