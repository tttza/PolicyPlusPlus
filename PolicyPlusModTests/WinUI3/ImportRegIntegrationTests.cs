using Microsoft.Win32;
using Xunit;

namespace PolicyPlusModTests.WinUI3
{
    public class ImportRegIntegrationTests
    {
        [Fact(DisplayName = ".reg Apply strips hive prefix and writes to sink")]
        public void RegFile_Apply_StripsHivePrefix()
        {
            var reg = new RegFile();
            // Build a reg with absolute hive names (as a user could load without proper prefix)
            reg.Keys.Add(
                new RegFile.RegFileKey
                {
                    Name = "HKEY_LOCAL_MACHINE\\Software\\Policies\\PolicyPlusTest",
                    Values =
                    {
                        new RegFile.RegFileValue
                        {
                            Name = "Test",
                            Kind = RegistryValueKind.DWord,
                            Data = 1u,
                        },
                    },
                }
            );

            var sink = new PolFile();
            reg.Apply(sink);

            Assert.True(sink.ContainsValue("Software\\Policies\\PolicyPlusTest", "Test"));
            Assert.Equal(1u, sink.GetValue("Software\\Policies\\PolicyPlusTest", "Test"));
        }
    }
}
