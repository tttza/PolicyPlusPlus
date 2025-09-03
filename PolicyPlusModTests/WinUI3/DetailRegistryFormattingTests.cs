using System;
using System.Collections.Generic;
using System.Linq;
using PolicyPlus;
using PolicyPlus.Core.Core;
using PolicyPlus.WinUI3.ViewModels;
using Xunit;

namespace PolicyPlusModTests.WinUI3
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
            var user = new PolFile();
            var p = MakeTogglePolicy("MACHINE:Toggle", AdmxPolicySection.Machine);

            // Write actual value
            comp.SetValue("Software\\PolicyPlusTest", "V", 1u, Microsoft.Win32.RegistryValueKind.DWord);

            var formatted = RegistryViewFormatter.BuildRegistryFormatted(p, comp, AdmxPolicySection.Machine);
            Assert.Contains("Type: REG_DWORD", formatted);
            Assert.Contains("Data:", formatted);

            var reg = RegistryViewFormatter.BuildRegExport(p, comp, AdmxPolicySection.Machine);
            Assert.Contains("Windows Registry Editor Version 5.00", reg);
            Assert.Contains("\"V\"=dword:00000001", reg);
        }
    }
}
