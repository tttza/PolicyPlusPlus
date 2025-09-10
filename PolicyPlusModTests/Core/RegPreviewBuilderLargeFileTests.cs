using System.IO;

using PolicyPlusCore.IO;
using PolicyPlusCore.Utilities;

using Xunit;

namespace PolicyPlusModTests.Core
{
    public class RegPreviewBuilderLargeFileTests
    {
        [Fact(DisplayName = "Preview includes HKCU section for multi-hive .reg files")]
        public void Preview_Includes_HKCU_For_MultiHive()
        {
            var content = "Windows Registry Editor Version 5.00\r\n\r\n"
                + "[HKEY_LOCAL_MACHINE\\SOFTWARE\\Policies\\Microsoft\\Peernet]\r\n\"Disabled\"=dword:00000000\r\n\r\n"
                + "[HKEY_CURRENT_USER\\SOFTWARE\\Policies\\Microsoft\\Edge]\r\n\"EnterpriseModeSiteListManagerAllowed\"=dword:00000001\r\n";
            var tmp = Path.GetTempFileName();
            File.WriteAllText(tmp, content);

            var reg = RegFile.Load(tmp, "");
            var text = RegPreviewBuilder.BuildPreview(reg);

            Assert.Contains("; HKLM", text);
            Assert.Contains("; HKCU", text);
            Assert.Contains("[HKEY_CURRENT_USER\\SOFTWARE\\Policies\\Microsoft\\Edge]", text);
        }
    }
}
