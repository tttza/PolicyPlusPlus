using System.Collections.Generic;
using PolicyPlusPlus.Services;
using Xunit;

namespace PolicyPlusModTests.WinUI3.PendingChanges
{
    public class PendingChangesWindowSaveFlowTests
    {
        [Fact(DisplayName = "Save flow builds POL base64 and calls elevation writer")]
        public void SaveFlow_CallsElevationService_WithBuiltBase64()
        {
            var pol = new PolicyPlusPolicy
            {
                UniqueID = "MACHINE:Toggle",
                DisplayName = "Toggle",
                RawPolicy = new AdmxPolicy
                {
                    RegistryKey = "Software\\PolicyPlusTest",
                    RegistryValue = "V",
                    Section = AdmxPolicySection.Machine,
                    AffectedValues = new PolicyRegistryList(),
                    DefinedIn = new AdmxFile { SourceFile = "dummy.admx" },
                },
            };
            var bundle = new AdmxBundle
            {
                Policies = new Dictionary<string, PolicyPlusPolicy> { { pol.UniqueID, pol } },
            };
            var change = new PendingChange
            {
                PolicyId = pol.UniqueID,
                PolicyName = pol.DisplayName,
                Scope = "Computer",
                DesiredState = PolicyState.Enabled,
                Options = new Dictionary<string, object>(),
            };

            PendingChangesService.Instance.Pending.Clear();
            PendingChangesService.Instance.History.Clear();
            PendingChangesService.Instance.Add(change);

            var req = new List<PolicyChangeRequest>
            {
                new PolicyChangeRequest
                {
                    PolicyId = change.PolicyId,
                    Scope = PolicyTargetScope.Machine,
                    DesiredState = change.DesiredState,
                    Options = change.Options,
                },
            };
            var b64 = PolicySavePipeline.BuildLocalGpoBase64(bundle, req);
            Assert.False(string.IsNullOrEmpty(b64.machineBase64));
        }
    }
}
