using System.Collections.Generic;
using PolicyPlus;
using PolicyPlus.WinUI3.ViewModels;
using Xunit;

namespace PolicyPlusModTests.WinUI3
{
    public class DetailPathTextTests
    {
        private static PolicyPlusPolicy MakePolicyWithCategoryChain()
        {
            var root = new PolicyPlusCategory { DisplayName = "Root", RawCategory = new AdmxCategory() };
            var mid = new PolicyPlusCategory { DisplayName = "Mid", RawCategory = new AdmxCategory(), Parent = root };
            var leaf = new PolicyPlusCategory { DisplayName = "Leaf", RawCategory = new AdmxCategory(), Parent = mid };

            return new PolicyPlusPolicy
            {
                UniqueID = "MACHINE:Sample",
                DisplayName = "SamplePolicy",
                Category = leaf,
                RawPolicy = new AdmxPolicy
                {
                    RegistryKey = "Software\\PolicyPlusTest",
                    RegistryValue = "V",
                    Section = AdmxPolicySection.Machine,
                    AffectedValues = new PolicyRegistryList(),
                    DefinedIn = new AdmxFile { SourceFile = "dummy.admx" }
                }
            };
        }

        [Fact(DisplayName = "Path text contains configuration scope and full category chain")]
        public void PathText_Includes_Scope_And_Categories()
        {
            var p = MakePolicyWithCategoryChain();
            var text = DetailPathFormatter.BuildPathText(p).Replace("\r\n", "\n");

            Assert.Contains("Computer Configuration", text);
            Assert.Contains("+ Administrative Templates", text);
            Assert.Contains("+ Root", text);
            Assert.Contains("+ Mid", text);
            Assert.Contains("+ Leaf", text);
            Assert.Contains("+ SamplePolicy", text);
        }
    }
}
