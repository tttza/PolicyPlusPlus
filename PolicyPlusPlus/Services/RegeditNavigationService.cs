using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace PolicyPlusPlus.Services
{
    public static class RegeditNavigationService
    {
        // Try to open regedit non-elevated; returns true if launch was initiated
        private static bool TryOpenAtKey(string hive, string subKey)
        {
            var hiveName = NormalizeHive(hive);
            var normalizedSub = NormalizeKey(subKey ?? string.Empty);
            try
            {
                var existingPath = GetDeepestExistingSubKey(hiveName, normalizedSub);
                var targetPath = string.IsNullOrEmpty(existingPath)
                    ? hiveName
                    : $"{hiveName}\\{existingPath}";

                const string regeditKeyPath =
                    @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Applets\Regedit";
                Registry.SetValue(regeditKeyPath, "DisableLastKey", 0, RegistryValueKind.DWord);
                string prefixStyle = TryGetCurrentLastKeyPrefixStyle();
                string lastKeyToSet = prefixStyle + targetPath;
                Registry.SetValue(
                    regeditKeyPath,
                    "LastKey",
                    lastKeyToSet,
                    RegistryValueKind.String
                );

                var psi = new ProcessStartInfo
                {
                    FileName = "regedit.exe",
                    Arguments = "/m",
                    UseShellExecute = true,
                };
                var p = Process.Start(psi);
                return p != null;
            }
            catch
            {
                return false;
            }
        }

        public static void OpenAtKey(string hive, string subKey)
        {
            TryOpenAtKey(hive, subKey);
        }

        public static async Task OpenAtKeyAsync(string hive, string subKey)
        {
            if (string.IsNullOrWhiteSpace(hive))
                return;
            subKey ??= string.Empty;
            var hiveName = NormalizeHive(hive);
            var normalizedSub = NormalizeKey(subKey);

            try
            {
                var res = await ElevationService
                    .Instance.OpenRegeditAtAsync(hiveName, normalizedSub)
                    .ConfigureAwait(false);
                if (res.Ok)
                    return;
            }
            catch { }

            TryOpenAtKey(hiveName, normalizedSub);
        }

        private static string TryGetCurrentLastKeyPrefixStyle()
        {
            try
            {
                using var regedit = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Applets\Regedit",
                    writable: false
                );
                var val = regedit?.GetValue("LastKey") as string ?? string.Empty;
                if (val.StartsWith("Computer\\", StringComparison.OrdinalIgnoreCase))
                    return "Computer\\";
            }
            catch { }
            return string.Empty;
        }

        private static string NormalizeHive(string hive)
        {
            hive = (hive ?? string.Empty).Trim();
            if (hive.Equals("HKLM", StringComparison.OrdinalIgnoreCase))
                return "HKEY_LOCAL_MACHINE";
            if (hive.Equals("HKCU", StringComparison.OrdinalIgnoreCase))
                return "HKEY_CURRENT_USER";
            if (hive.Equals("HKEY_LOCAL_MACHINE", StringComparison.OrdinalIgnoreCase))
                return "HKEY_LOCAL_MACHINE";
            if (hive.Equals("HKEY_CURRENT_USER", StringComparison.OrdinalIgnoreCase))
                return "HKEY_CURRENT_USER";
            return hive;
        }

        private static string NormalizeKey(string key)
        {
            key = (key ?? string.Empty).Replace('/', '\\').Trim();
            if (key.StartsWith("\\"))
                key = key.TrimStart('\\');
            return key;
        }

        private static string GetDeepestExistingSubKey(string hiveName, string subPath)
        {
            try
            {
                using var baseKey = hiveName.Equals(
                    "HKEY_LOCAL_MACHINE",
                    StringComparison.OrdinalIgnoreCase
                )
                    ? RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Default)
                    : RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default);

                if (string.IsNullOrEmpty(subPath))
                    return string.Empty;

                var parts = subPath.Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);
                string deepest = string.Empty;
                RegistryKey? current = baseKey;

                foreach (var p in parts)
                {
                    if (current == null)
                        break;
                    var next = current.OpenSubKey(p);
                    if (next == null)
                        break;
                    deepest = string.IsNullOrEmpty(deepest) ? p : $"{deepest}\\{p}";
                    current = next;
                }

                return deepest;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
