using System.IO;

using Microsoft.Win32;

using PolicyPlus.Core.IO;

using Xunit;

namespace PolicyPlusModTests.Core
{
    public class RegFileExportFormatTests
    {
        [Fact(DisplayName = "Exported .reg canonicalizes key casing and removes leading backslash")]
        public void RegFile_Save_CanonicalizesAndNoLeadingBackslash()
        {
            var rf = new RegFile();
            // Simulate a snapshot that accidentally produces a leading backslash and lower-case segments
            rf.Keys.Add(new RegFile.RegFileKey
            {
                Name = "\\HKEY_LOCAL_MACHINE\\software\\policies\\Microsoft\\Peernet",
                Values = { new RegFile.RegFileValue { Name = "Disabled", Kind = RegistryValueKind.DWord, Data = 0u } }
            });

            using var sw = new StringWriter();
            rf.Save(sw);
            var text = sw.ToString();

            Assert.Contains("[HKEY_LOCAL_MACHINE\\SOFTWARE\\Policies\\Microsoft\\Peernet]", text);
            Assert.Contains("\"Disabled\"=dword:00000000", text);
        }
    }
}
