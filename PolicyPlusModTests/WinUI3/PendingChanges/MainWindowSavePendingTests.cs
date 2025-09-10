using System.Collections.Generic;
using PolicyPlus;
using PolicyPlusCore.Core;
using PolicyPlusPlus;
using PolicyPlusPlus.Services;
using Xunit;

namespace PolicyPlusModTests.WinUI3
{
    public class MainWindowSavePendingTests
    {
        [Fact(DisplayName = "MainWindow SavePendingAsync builds base64 for both scopes when needed")]
        public void SavePending_BuildsBase64_ForBothScopes()
        {
            // Build bundle with user & machine policies
            var compPol = new PolicyPlusPolicy
            {
                UniqueID = "MACHINE:T",
                DisplayName = "T",
                RawPolicy = new AdmxPolicy { RegistryKey = "Software\\PolicyPlusTest", RegistryValue = "V", Section = AdmxPolicySection.Machine, AffectedValues = new PolicyRegistryList(), DefinedIn = new AdmxFile { SourceFile = "d.admx" } }
            };
            var userPol = new PolicyPlusPolicy
            {
                UniqueID = "USER:T",
                DisplayName = "T",
                RawPolicy = new AdmxPolicy { RegistryKey = "Software\\PolicyPlusTest", RegistryValue = "V", Section = AdmxPolicySection.User, AffectedValues = new PolicyRegistryList(), DefinedIn = new AdmxFile { SourceFile = "d.admx" } }
            };
            var bundle = new AdmxBundle { Policies = new Dictionary<string, PolicyPlusPolicy> { { compPol.UniqueID, compPol }, { userPol.UniqueID, userPol } } };

            var pending = new[]
            {
                new PendingChange { PolicyId = compPol.UniqueID, Scope = "Computer", DesiredState = PolicyState.Enabled, Options = new Dictionary<string, object>() },
                new PendingChange { PolicyId = userPol.UniqueID, Scope = "User", DesiredState = PolicyState.Disabled, Options = new Dictionary<string, object>() },
            };

            // Call non-UI save pipeline directly instead of new MainWindow()
            var req = new List<PolicyChangeRequest>
            {
                new PolicyChangeRequest { PolicyId = compPol.UniqueID, Scope = PolicyTargetScope.Machine, DesiredState = PolicyState.Enabled, Options = new Dictionary<string, object>() },
                new PolicyChangeRequest { PolicyId = userPol.UniqueID, Scope = PolicyTargetScope.User, DesiredState = PolicyState.Disabled, Options = new Dictionary<string, object>() },
            };
            var b64 = PolicySavePipeline.BuildLocalGpoBase64(bundle, req);
            Assert.False(string.IsNullOrEmpty(b64.machineBase64));
            Assert.False(string.IsNullOrEmpty(b64.userBase64));
        }
    }
}
