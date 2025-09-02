using System;
using System.Linq;

namespace PolicyPlus.WinUI3.Services
{
    internal static class RegImportHelper
    {
        public static (RegFile userReg, RegFile machineReg) SplitByHive(RegFile source)
        {
            var user = new RegFile(); user.SetPrefix(string.Empty);
            var machine = new RegFile(); machine.SetPrefix(string.Empty);

            foreach (var key in source.Keys)
            {
                var name = key.Name ?? string.Empty;
                if (StartsWith(name, "HKEY_LOCAL_MACHINE\\") || StartsWith(name, "HKLM\\"))
                {
                    machine.Keys.Add(CloneKey(key));
                }
                else if (StartsWith(name, "HKEY_CURRENT_USER\\") || StartsWith(name, "HKCU\\") || StartsWith(name, "HKEY_USERS\\") || StartsWith(name, "HKU\\"))
                {
                    user.Keys.Add(CloneKey(key));
                }
                else
                {
                    // Unknown root: ignore for safety
                }
            }
            return (user, machine);
        }

        public static (PolFile userPol, PolFile machinePol) ToPolByHive(RegFile source)
        {
            var (userReg, machineReg) = SplitByHive(source);
            var userPol = new PolFile();
            var machinePol = new PolFile();
            try { if (userReg.Keys.Count > 0) userReg.Apply(userPol); } catch { }
            try { if (machineReg.Keys.Count > 0) machineReg.Apply(machinePol); } catch { }
            return (userPol, machinePol);
        }

        private static bool StartsWith(string text, string prefix)
            => text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);

        private static RegFile.RegFileKey CloneKey(RegFile.RegFileKey src)
        {
            var k = new RegFile.RegFileKey { Name = src.Name, IsDeleter = src.IsDeleter };
            foreach (var v in src.Values)
            {
                k.Values.Add(new RegFile.RegFileValue { Name = v.Name, Data = v.Data, Kind = v.Kind, IsDeleter = v.IsDeleter });
            }
            return k;
        }
    }
}
