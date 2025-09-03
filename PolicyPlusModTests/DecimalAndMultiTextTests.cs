using System.Collections.Generic;
using PolicyPlus;
using Xunit;
using PolicyPlusModTests.TestHelpers;
using PolicyPlus.Core.Core;

namespace PolicyPlusModTests
{
    public class DecimalAndMultiTextTests
    {
        [Fact(DisplayName = "Decimal element (DWORD) writes and reads correctly")]
        public void DecimalElement_WritesDword_And_ReadsBack()
        {
            var polFile = new PolFile();
            var decElem = new DecimalPolicyElement
            {
                ID = "Dec",
                ElementType = "decimal",
                RegistryKey = "Software\\PolicyPlusTest",
                RegistryValue = "DecValue",
                StoreAsText = false
            };
            var raw = new AdmxPolicy
            {
                RegistryKey = "Software\\PolicyPlusTest",
                RegistryValue = "DecValue",
                Section = AdmxPolicySection.Machine,
                Elements = new List<PolicyElement> { decElem },
                AffectedValues = new PolicyRegistryList(),
                DefinedIn = new AdmxFile { SourceFile = "dummy.admx" }
            };
            var policy = new PolicyPlusPolicy { RawPolicy = raw, UniqueID = "MACHINE:DecDword", DisplayName = "Dec DWord" };

            PolicyProcessing.SetPolicyState(polFile, policy, PolicyState.Enabled, new Dictionary<string, object> { { "Dec", 123u } });
            PolAssert.HasDwordValue(polFile, raw.RegistryKey, raw.RegistryValue, 123u);

            var states = PolicyProcessing.GetPolicyOptionStates(polFile, policy);
            Assert.Equal(123u, (uint)states["Dec"]);
        }

        [Fact(DisplayName = "Decimal element (StoreAsText) writes REG_SZ and reads number")]
        public void DecimalElement_StoreAsText_WritesString_And_ReadsBackNumber()
        {
            var polFile = new PolFile();
            var decElem = new DecimalPolicyElement
            {
                ID = "Dec",
                ElementType = "decimal",
                RegistryKey = "Software\\PolicyPlusTest",
                RegistryValue = "DecText",
                StoreAsText = true
            };
            var raw = new AdmxPolicy
            {
                RegistryKey = "Software\\PolicyPlusTest",
                RegistryValue = "DecText",
                Section = AdmxPolicySection.Machine,
                Elements = new List<PolicyElement> { decElem },
                AffectedValues = new PolicyRegistryList(),
                DefinedIn = new AdmxFile { SourceFile = "dummy.admx" }
            };
            var policy = new PolicyPlusPolicy { RawPolicy = raw, UniqueID = "MACHINE:DecText", DisplayName = "Dec Text" };

            PolicyProcessing.SetPolicyState(polFile, policy, PolicyState.Enabled, new Dictionary<string, object> { { "Dec", 456u } });
            PolAssert.HasStringValue(polFile, raw.RegistryKey, raw.RegistryValue, "456");

            var states = PolicyProcessing.GetPolicyOptionStates(polFile, policy);
            Assert.Equal(456u, (uint)states["Dec"]);
        }

        [Fact(DisplayName = "Decimal element (StoreAsText) invalid numeric reads as 0")]
        public void DecimalElement_StoreAsText_InvalidNumber_ReadsZero()
        {
            var polFile = new PolFile();
            var decElem = new DecimalPolicyElement
            {
                ID = "Dec",
                ElementType = "decimal",
                RegistryKey = "Software\\PolicyPlusTest",
                RegistryValue = "DecTextInvalid",
                StoreAsText = true
            };
            var raw = new AdmxPolicy
            {
                RegistryKey = "Software\\PolicyPlusTest",
                RegistryValue = "DecTextInvalid",
                Section = AdmxPolicySection.Machine,
                Elements = new List<PolicyElement> { decElem },
                AffectedValues = new PolicyRegistryList(),
                DefinedIn = new AdmxFile { SourceFile = "dummy.admx" }
            };
            var policy = new PolicyPlusPolicy { RawPolicy = raw, UniqueID = "MACHINE:DecTextInvalid", DisplayName = "Dec Text Invalid" };

            // write invalid numeric as text
            PolicyProcessing.SetPolicyState(polFile, policy, PolicyState.Enabled, new Dictionary<string, object> { { "Dec", "notANumber" } });
            PolAssert.HasStringValue(polFile, raw.RegistryKey, raw.RegistryValue, "notANumber");

            var states = PolicyProcessing.GetPolicyOptionStates(polFile, policy);
            Assert.Equal(0u, (uint)states["Dec"]);
        }

        [Fact(DisplayName = "MultiText element accepts IEnumerable<string> input")]
        public void MultiText_Accepts_IEnumerable()
        {
            var polFile = new PolFile();
            var multiElem = new MultiTextPolicyElement
            {
                ID = "Multi",
                ElementType = "multiText",
                RegistryKey = "Software\\PolicyPlusTest",
                RegistryValue = "MultiValue"
            };
            var raw = new AdmxPolicy
            {
                RegistryKey = "Software\\PolicyPlusTest",
                RegistryValue = "MultiValue",
                Section = AdmxPolicySection.Machine,
                Elements = new List<PolicyElement> { multiElem },
                AffectedValues = new PolicyRegistryList(),
                DefinedIn = new AdmxFile { SourceFile = "dummy.admx" }
            };
            var policy = new PolicyPlusPolicy { RawPolicy = raw, UniqueID = "MACHINE:Multi", DisplayName = "Multi" };

            IEnumerable<string> lines = new List<string> { "x", "y" };
            PolicyProcessing.SetPolicyState(polFile, policy, PolicyState.Enabled, new Dictionary<string, object> { { "Multi", lines } });
            PolAssert.HasMultiStringValue(polFile, raw.RegistryKey, raw.RegistryValue, lines);
        }
    }
}
