using System;
using System.IO;
using PolicyPlusPlus.Services;
using PolicyPlusCore.IO;
using Xunit;

namespace PolicyPlusModTests.WinUI3
{
    // Ensure singleton PolicySourceManager is not accessed concurrently.
    [Collection("PolicySourceManagerSerial")] 
    public class PolicySourceManagerTests
    {
        private readonly IPolicySourceManager _mgr = PolicySourceManager.Instance as IPolicySourceManager;

        [Fact(DisplayName = "Switch to LocalGpo sets mode")]
        public void Switch_LocalGpo_SetsMode()
        {
            var ok = _mgr.Switch(PolicySourceDescriptor.LocalGpo());
            Assert.True(ok);
            Assert.Equal(PolicySourceMode.LocalGpo, _mgr.Mode);
        }

        [Fact(DisplayName = "Switch to TempPol provides pol sources")] 
        public void Switch_TempPol_ProvidesPolSources()
        {
            var ok = _mgr.Switch(PolicySourceDescriptor.TempPol());
            Assert.True(ok);
            Assert.Equal(PolicySourceMode.TempPol, _mgr.Mode);
            Assert.IsType<PolFile>(_mgr.CompSource);
            Assert.IsType<PolFile>(_mgr.UserSource);
        }

        [Fact(DisplayName = "Switch to CustomPol creates files and loads sources")]
        public void Switch_CustomPol_CreatesAndLoads()
        {
            var baseDir = Path.Combine(Path.GetTempPath(), "PolicyPlusTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(baseDir);
            var compPath = Path.Combine(baseDir, "machine.pol");
            var userPath = Path.Combine(baseDir, "user.pol");
            Assert.False(File.Exists(compPath));
            Assert.False(File.Exists(userPath));

            var ok = _mgr.Switch(PolicySourceDescriptor.Custom(compPath, userPath));
            Assert.True(ok);
            Assert.Equal(PolicySourceMode.CustomPol, _mgr.Mode);
            Assert.True(File.Exists(compPath));
            Assert.True(File.Exists(userPath));
            Assert.IsType<PolFile>(_mgr.CompSource);
            Assert.IsType<PolFile>(_mgr.UserSource);
        }

        [Fact(DisplayName = "Switch CustomPol invalid when path missing")]
        public void Switch_CustomPol_InvalidMissingPath()
        {
            var baseDir = Path.Combine(Path.GetTempPath(), "PolicyPlusTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(baseDir);
            var compPath = Path.Combine(baseDir, "machine.pol");
            var userPath = Path.Combine(baseDir, "user.pol");
            var okValid = _mgr.Switch(PolicySourceDescriptor.Custom(compPath, userPath));
            Assert.True(okValid);
            var prevMode = _mgr.Mode;

            var invalidUserPath = "   "; // whitespace path triggers guard
            var okInvalid = _mgr.Switch(PolicySourceDescriptor.Custom(compPath, invalidUserPath));
            Assert.False(okInvalid); // should be rejected without exception
            Assert.Equal(prevMode, _mgr.Mode); // mode unchanged
        }
    }

    [CollectionDefinition("PolicySourceManagerSerial", DisableParallelization = true)]
    public class PolicySourceManagerSerialCollection { }
}
