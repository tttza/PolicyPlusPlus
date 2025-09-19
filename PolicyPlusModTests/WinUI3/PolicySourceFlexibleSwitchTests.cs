using System;
using System.IO;
using PolicyPlusPlus.Services;
using Xunit;

namespace PolicyPlusModTests.WinUI3
{
    [Collection("PolicySourceManagerSerial")]
    public class PolicySourceFlexibleSwitchTests
    {
        private readonly IPolicySourceManager _mgr = PolicySourceManager.Instance;

        [Fact(
            DisplayName = "Flexible custom switch with only computer path generates user placeholder"
        )]
        public void SwitchCustom_ComputerOnly_Completes()
        {
            _mgr.Switch(PolicySourceDescriptor.LocalGpo());
            var baseDir = Path.Combine(
                Path.GetTempPath(),
                "PolicyPlusFlexTests",
                Guid.NewGuid().ToString("N")
            );
            Directory.CreateDirectory(baseDir);
            var comp = Path.Combine(baseDir, "machine.pol");
            // Let manager create file; do not pre-create empty invalid POL.
            var ok = _mgr.SwitchCustomPolFlexible(comp, null, allowSingle: true);
            Assert.True(ok);
            Assert.Equal(PolicySourceMode.CustomPol, _mgr.Mode);
            Assert.NotNull(_mgr.CustomUserPath);
            Assert.True(File.Exists(_mgr.CustomUserPath));
        }

        [Fact(
            DisplayName = "Flexible custom switch with only user path generates computer placeholder"
        )]
        public void SwitchCustom_UserOnly_Completes()
        {
            _mgr.Switch(PolicySourceDescriptor.LocalGpo());
            var baseDir = Path.Combine(
                Path.GetTempPath(),
                "PolicyPlusFlexTests",
                Guid.NewGuid().ToString("N")
            );
            Directory.CreateDirectory(baseDir);
            var user = Path.Combine(baseDir, "user.pol");
            var ok = _mgr.SwitchCustomPolFlexible(null, user, allowSingle: true);
            Assert.True(ok);
            Assert.Equal(PolicySourceMode.CustomPol, _mgr.Mode);
            Assert.NotNull(_mgr.CustomCompPath);
            Assert.True(File.Exists(_mgr.CustomCompPath));
        }
    }

    [CollectionDefinition("PolicySourceManagerSerial", DisableParallelization = true)]
    public class PolicySourceManagerSerialCollection2 { }
}
