using PolicyPlusPlus.Services;
using PolicyPlusPlus.ViewModels;
using Xunit;

namespace PolicyPlusModTests.WinUI3
{
    public class CustomPolViewModelTests
    {
        [Fact]
        public void SinglePathEnable_CompletesFlexibleSwitchPreconditions()
        {
            var s = SettingsService.Instance.LoadSettings();
            var vm = new CustomPolViewModel(SettingsService.Instance, s.CustomPol);
            vm.EnableComputer = true;
            vm.ComputerPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cpol_vm_test.pol");
            vm.Active = true;
            Assert.True(vm.IsDirty);
            vm.Commit();
            Assert.False(vm.IsDirty);
            var after = SettingsService.Instance.LoadSettings();
            Assert.True(after.CustomPol!.EnableComputer);
            Assert.True(after.CustomPol!.Active);
        }

        [Fact]
        public void DisableThenCommit_ClearsActive()
        {
            var s = SettingsService.Instance.LoadSettings();
            var vm = new CustomPolViewModel(SettingsService.Instance, s.CustomPol);
            vm.EnableComputer = true; vm.ComputerPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cpol_vm_test2.pol"); vm.Active = true; vm.Commit();
            vm.EnableComputer = false; vm.EnableUser = false; vm.Active = false;
            Assert.True(vm.IsDirty);
            vm.Commit();
            var after = SettingsService.Instance.LoadSettings();
            Assert.False(after.CustomPol!.Active);
            Assert.False(after.CustomPol!.EnableComputer);
        }
    }
}
