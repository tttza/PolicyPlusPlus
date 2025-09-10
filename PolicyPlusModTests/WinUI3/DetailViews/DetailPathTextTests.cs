using System.Collections.Generic;
using PolicyPlus;
using PolicyPlus.Core.Core;
using PolicyPlusPlus.ViewModels;
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

            // Allow either English or Japanese (or other future) localization for the top labels
            bool hasConfig = text.Contains("Computer Configuration") || text.Contains("コンピューターの構成") || text.ToLower().Contains("configuration");
            Assert.True(hasConfig, "Configuration scope label missing");

            bool hasTemplates = text.Contains("Administrative Templates") || text.Contains("管理用テンプレート") || text.ToLower().Contains("template");
            Assert.True(hasTemplates, "Administrative Templates label missing");

            Assert.Contains("+ Root", text);
            Assert.Contains("+ Mid", text);
            Assert.Contains("+ Leaf", text);
            Assert.Contains("SamplePolicy", text);
        }
    }
}
