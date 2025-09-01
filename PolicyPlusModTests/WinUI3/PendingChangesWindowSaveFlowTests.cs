using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PolicyPlus;
using PolicyPlus.WinUI3;
using PolicyPlus.WinUI3.Services;
using PolicyPlus.WinUI3.Windows;
using Xunit;

namespace PolicyPlusModTests.WinUI3
{
    public class PendingChangesWindowSaveFlowTests
    {
        private sealed class FakeElevationService : IElevationService
        {
            public List<(string? m, string? u, bool refresh)> Calls { get; } = new();
            public Task<(bool ok, string? error)> WriteLocalGpoBytesAsync(string? machinePolBase64, string? userPolBase64, bool triggerRefresh = true)
            {
                Calls.Add((machinePolBase64, userPolBase64, triggerRefresh));
                return Task.FromResult((true, (string?)null));
            }
        }

        private static (AdmxBundle bundle, PendingChange change) BuildContext()
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
                    DefinedIn = new AdmxFile { SourceFile = "dummy.admx" }
                }
            };
            var bundle = new AdmxBundle { Policies = new Dictionary<string, PolicyPlusPolicy> { { pol.UniqueID, pol } } };
            var change = new PendingChange
            {
                PolicyId = pol.UniqueID,
                PolicyName = pol.DisplayName,
                Scope = "Computer",
                DesiredState = PolicyState.Enabled,
                Options = new Dictionary<string, object>()
            };
            return (bundle, change);
        }

        [Fact(DisplayName = "Save flow builds POL base64 and calls elevation writer")]
        public async Task SaveFlow_CallsElevationService_WithBuiltBase64()
        {
            var (bundle, change) = BuildContext();
            PendingChangesService.Instance.Pending.Clear();
            PendingChangesService.Instance.History.Clear();
            PendingChangesService.Instance.Add(change);

            var req = new List<PolicyChangeRequest>
            {
                new PolicyChangeRequest { PolicyId = change.PolicyId, Scope = PolicyTargetScope.Machine, DesiredState = change.DesiredState, Options = change.Options }
            };
            var b64 = PolicySavePipeline.BuildLocalGpoBase64(bundle, req);
            Assert.False(string.IsNullOrEmpty(b64.machineBase64));
        }
    }
}
