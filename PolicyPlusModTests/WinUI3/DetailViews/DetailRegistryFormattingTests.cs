using Microsoft.Win32;
using PolicyPlusModTests.Testing;
using PolicyPlusPlus.ViewModels;
using System.Collections.Generic;
using Xunit;

namespace PolicyPlusModTests.WinUI3.DetailViews
{
    public class DetailRegistryFormattingTests
    {
        private static PolicyPlusPolicy MakeTogglePolicy(string id, AdmxPolicySection section)
        {
            return new PolicyPlusPolicy
            {
                UniqueID = id,
                DisplayName = "Policy",
                RawPolicy = new AdmxPolicy
                {
                    RegistryKey = "Software\\PolicyPlusTest",
                    RegistryValue = "V",
                    Section = section,
                    AffectedValues = new PolicyRegistryList(),
                    DefinedIn = new AdmxFile { SourceFile = "dummy.admx" }
                },
                Category = new PolicyPlusCategory { DisplayName = "Cat", RawCategory = new AdmxCategory() }
            };
        }

        [Fact(DisplayName = "Formatted view shows REG_DWORD for enabled toggle policy")]
        public void Formatted_Reflects_ActualRegistry()
        {
            var comp = new PolFile();
            var p = MakeTogglePolicy("MACHINE:Toggle", AdmxPolicySection.Machine);
            comp.SetValue("Software\\PolicyPlusTest", "V", 1u, RegistryValueKind.DWord);

            var formatted = RegistryViewFormatter.BuildRegistryFormatted(p, comp, AdmxPolicySection.Machine);
            // Accept English or Japanese (or future) localization for labels. We specifically check that a REG_DWORD type line exists.
            Assert.Contains("REG_DWORD", formatted);
            // Verify the value name is present
            Assert.Contains("V", formatted);

            var reg = RegistryViewFormatter.BuildRegExport(p, comp, AdmxPolicySection.Machine);
            Assert.Contains("Windows Registry Editor Version 5.00", reg);
            Assert.Contains("\"V\"=dword:00000001", reg);
        }

        [Fact(DisplayName = "Formatted view shows list items for list policy (prefix mode)")]
        public void Formatted_Shows_ListItems()
        {
            var polFile = new PolFile();
            var policy = TestPolicyFactory.CreateListPolicy();
            var items = new List<string> { "Alpha", "Beta", "Gamma" };
            PolicyProcessing.SetPolicyState(polFile, policy, PolicyState.Enabled, new Dictionary<string, object> { { "ListElem", items } });

            var formatted = RegistryViewFormatter.BuildRegistryFormatted(policy, polFile, AdmxPolicySection.Machine);
            // Expect each generated value name (ListPrefix1, ListPrefix2, ...) and its data to appear.
            Assert.Contains("ListPrefix1", formatted);
            Assert.Contains("ListPrefix2", formatted);
            Assert.Contains("ListPrefix3", formatted);
            Assert.Contains("Alpha", formatted);
            Assert.Contains("Beta", formatted);
            Assert.Contains("Gamma", formatted);

            var reg = RegistryViewFormatter.BuildRegExport(policy, polFile, AdmxPolicySection.Machine);
            Assert.Contains("\"ListPrefix1\"=\"Alpha\"", reg);
            Assert.Contains("\"ListPrefix2\"=\"Beta\"", reg);
            Assert.Contains("\"ListPrefix3\"=\"Gamma\"", reg);
        }

        [Fact(DisplayName = "Formatted view shows REG_MULTI_SZ for multiText element")]
        public void Formatted_Shows_MultiText()
        {
            var polFile = new PolFile();
            var policy = TestPolicyFactory.CreateMultiTextPolicy();
            var lines = new[] { "line1", "second line" };
            PolicyProcessing.SetPolicyState(polFile, policy, PolicyState.Enabled, new Dictionary<string, object> { { "MultiTextElem", lines } });

            var formatted = RegistryViewFormatter.BuildRegistryFormatted(policy, polFile, AdmxPolicySection.Machine);
            Assert.Contains("REG_MULTI_SZ", formatted);
            Assert.Contains("line1", formatted);
            Assert.Contains("second line", formatted);
        }

        [Fact(DisplayName = "Formatted view shows REG_QWORD for QWORD policy value")]
        public void Formatted_Shows_Qword()
        {
            var polFile = new PolFile();
            var policy = TestPolicyFactory.CreateQwordPolicy();
            polFile.SetValue(policy.RawPolicy.RegistryKey, policy.RawPolicy.RegistryValue, 1234567890123456789UL, RegistryValueKind.QWord);

            var formatted = RegistryViewFormatter.BuildRegistryFormatted(policy, polFile, AdmxPolicySection.Machine);
            Assert.Contains("REG_QWORD", formatted);
            Assert.Contains("1234567890123456789", formatted);
        }

        [Fact(DisplayName = "Formatted view shows REG_BINARY for binary policy value")]
        public void Formatted_Shows_Binary()
        {
            var polFile = new PolFile();
            var policy = TestPolicyFactory.CreateBinaryPolicy();
            var data = new byte[] { 0x01, 0x02, 0x0A, 0xFF };
            polFile.SetValue(policy.RawPolicy.RegistryKey, policy.RawPolicy.RegistryValue, data, RegistryValueKind.Binary);

            var formatted = RegistryViewFormatter.BuildRegistryFormatted(policy, polFile, AdmxPolicySection.Machine);
            Assert.Contains("REG_BINARY", formatted);
            Assert.Contains("01 02 0a ff", formatted); // lower-case hex
        }
    }
}
