using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PolicyPlusCore.Core;
using PolicyPlusCore.IO;
using PolicyPlusModTests.Testing;
using PolicyPlusPlus.Services;
using Xunit;

namespace PolicyPlusModTests.WinUI3.CustomPol
{
    public class CustomPolInitialEditTests
    {
        [Fact]
        public void AcquireSources_DoesNotDowngradeCustomPolAndReflectsExistingState()
        {
            // Prepare temp custom pol files
            var dir = Path.Combine(Path.GetTempPath(), "PPlusTests_CustomPol" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            var compPath = Path.Combine(dir, "machine.pol");
            var userPath = Path.Combine(dir, "user.pol");
            var compPol = new PolFile();
            var userPol = new PolFile();
            compPol.Save(compPath);
            userPol.Save(userPath);

            // Build a simple user-scope policy (use Section=Both so user side applies) 
            var policy = TestPolicyFactory.CreateSimpleTogglePolicy(uniqueId: "BOTH:TestCustomPolInitial", displayName: "Test CustomPol Initial");
            policy.RawPolicy.Section = AdmxPolicySection.Both;

            // Enable it in user pol prior to any UI acquisition
            PolicyProcessing.SetPolicyState(userPol, policy, PolicyState.Enabled, new System.Collections.Generic.Dictionary<string, object>());
            userPol.Save(userPath);

            // Switch manager to custom pol (allow single ensures creation even if one missing)
            var mgr = PolicySourceManager.Instance as PolicySourceManager; // concrete
            Assert.True(PolicySourceManager.Instance.SwitchCustomPolFlexible(compPath, userPath, allowSingle: true));
            Assert.Equal(PolicySourceMode.CustomPol, PolicySourceManager.Instance.Mode);

            // Force refresh of internal sources (simulate early bind)
            var ctx = PolicySourceAccessor.Acquire();
            Assert.Equal(PolicySourceMode.CustomPol, ctx.Mode); // must not downgrade
            Assert.False(ctx.FallbackUsed);

            // Validate that user source reports Enabled
            var stateUser = PolicyProcessing.GetPolicyState(ctx.User, policy);
            Assert.Equal(PolicyState.Enabled, stateUser);
        }
    }
}
