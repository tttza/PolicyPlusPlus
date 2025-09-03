using System.IO;

using PolicyPlus.Core.IO;
using PolicyPlus.Core.Utilities;

using Xunit;

namespace PolicyPlusModTests.WinUI3
{
    public class ImportRegDialogPreviewTests
    {
        [Fact(DisplayName = "RegPreviewBuilder shows HKCU keys when present")]
        public void Preview_Shows_HKCU_Group()
        {
            var tmp = Path.GetTempFileName();
            File.WriteAllText(tmp, "Windows Registry Editor Version 5.00\r\n\r\n[HKEY_CURRENT_USER\\Software\\Policies\\PolicyPlusTest]\r\n\"Test\"=dword:00000001\r\n");

            var reg = RegFile.Load(tmp, "");
            var text = RegPreviewBuilder.BuildPreview(reg);

            Assert.Contains("; HKCU", text);
            Assert.Contains("[HKEY_CURRENT_USER\\Software\\Policies\\PolicyPlusTest]", text);
        }
    }
}
