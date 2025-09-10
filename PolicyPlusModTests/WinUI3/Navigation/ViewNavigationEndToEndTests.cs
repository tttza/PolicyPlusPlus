using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PolicyPlus;
using PolicyPlus.Core.Admx;
using PolicyPlus.Core.Core;
using PolicyPlusPlus;
using PolicyPlusPlus.Services;
using Xunit;

namespace PolicyPlusModTests.WinUI3
{
    public class ViewNavigationEndToEndTests
    {
        private static (AdmxBundle bundle, PolicyPlusCategory catA, PolicyPlusCategory catB, PolicyPlusPolicy polA, PolicyPlusPolicy polB) BuildBundle()
        {
            var catA = new PolicyPlusCategory { UniqueID = "CAT:A", DisplayName = "A" };
            var catB = new PolicyPlusCategory { UniqueID = "CAT:B", DisplayName = "B" };
            catA.Children = new List<PolicyPlusCategory>();
            catB.Children = new List<PolicyPlusCategory>();

            var polA = new PolicyPlusPolicy
            {
                UniqueID = "MACHINE:PA",
                DisplayName = "PA",
                Category = catA,
                RawPolicy = new AdmxPolicy { RegistryKey = "HKLM\\Software", RegistryValue = "V", Section = AdmxPolicySection.Machine, AffectedValues = new PolicyRegistryList(), DefinedIn = new AdmxFile { SourceFile = "d.admx" } }
            };
            var polB = new PolicyPlusPolicy
            {
                UniqueID = "USER:PB",
                DisplayName = "PB",
                Category = catB,
                RawPolicy = new AdmxPolicy { RegistryKey = "HKCU\\Software", RegistryValue = "V", Section = AdmxPolicySection.User, AffectedValues = new PolicyRegistryList(), DefinedIn = new AdmxFile { SourceFile = "d.admx" } }
            };
            catA.Policies = new List<PolicyPlusPolicy> { polA };
            catB.Policies = new List<PolicyPlusPolicy> { polB };

            var bundle = new AdmxBundle
            {
                Categories = new Dictionary<string, PolicyPlusCategory>(StringComparer.OrdinalIgnoreCase)
                {
                    { catA.UniqueID, catA },
                    { catB.UniqueID, catB }
                },
                Policies = new Dictionary<string, PolicyPlusPolicy>(StringComparer.OrdinalIgnoreCase)
                {
                    { polA.UniqueID, polA },
                    { polB.UniqueID, polB }
                }
            };
            return (bundle, catA, catB, polA, polB);
        }

        [Fact(DisplayName = "Navigation back/forward applies category filter correctly")]
        public void BackForward_AppliesCategory()
        {
            ViewNavigationService.Instance.Clear();

            // Simulate user flow by pushing states directly (UI coupling is heavy to boot in tests)
            var (bundle, catA, catB, _, _) = BuildBundle();

            // Baseline state (no category)
            ViewNavigationService.Instance.Push(ViewState.Create(null, string.Empty, AdmxPolicySection.Both, false));
            // Navigate to A
            ViewNavigationService.Instance.Push(ViewState.Create(catA.UniqueID, string.Empty, AdmxPolicySection.Both, false));
            // Navigate to B
            ViewNavigationService.Instance.Push(ViewState.Create(catB.UniqueID, string.Empty, AdmxPolicySection.Both, false));

            // Back should return to A
            var s1 = ViewNavigationService.Instance.GoBack();
            Assert.NotNull(s1);
            Assert.Equal(catA.UniqueID, s1!.CategoryId);

            // Back should return to baseline
            var s0 = ViewNavigationService.Instance.GoBack();
            Assert.NotNull(s0);
            Assert.Null(s0!.CategoryId);

            // Forward to A again
            var s2 = ViewNavigationService.Instance.GoForward();
            Assert.NotNull(s2);
            Assert.Equal(catA.UniqueID, s2!.CategoryId);
        }
    }
}
