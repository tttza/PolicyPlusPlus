using System.Diagnostics;
using Microsoft.Win32;
using System;
using System.Threading.Tasks;

namespace PolicyPlus.WinUI3.Services
{
    public static class RegeditNavigationService
    {
        public static void OpenAtKey(string hive, string subKey)
        {
            if (string.IsNullOrWhiteSpace(hive)) return;
            subKey ??= string.Empty;

            try
            {
                var hiveName = NormalizeHive(hive);
                var normalizedSub = NormalizeKey(subKey);
                var existingPath = GetDeepestExistingSubKey(hiveName, normalizedSub);
                var targetPath = string.IsNullOrEmpty(existingPath) ? hiveName : $"{hiveName}\\{existingPath}";

                const string regeditKeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Applets\Regedit";

                // Ensure LastKey feature is enabled
                Registry.SetValue(regeditKeyPath, "DisableLastKey", 0, RegistryValueKind.DWord);

                // Choose prefix style based on current LastKey format (with or without 'Computer\')
                string prefixStyle = TryGetCurrentLastKeyPrefixStyle();
                string lastKeyToSet = prefixStyle + targetPath;

                Registry.SetValue(regeditKeyPath, "LastKey", lastKeyToSet, RegistryValueKind.String);

                var psi = new ProcessStartInfo
                {
                    FileName = "regedit.exe",
                    Arguments = "/m",
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch
            {
            }
        }

        public static async Task OpenAtKeyAsync(string hive, string subKey)
        {
            if (string.IsNullOrWhiteSpace(hive)) return;
            subKey ??= string.Empty;
            try
            {
                var (ok, _) = await ElevationService.Instance.OpenRegeditAtAsync(NormalizeHive(hive), NormalizeKey(subKey)).ConfigureAwait(false);
                if (!ok)
                {
                    OpenAtKey(hive, subKey);
                }
            }
            catch
            {
                OpenAtKey(hive, subKey);
            }
        }

        private static string TryGetCurrentLastKeyPrefixStyle()
        {
            try
            {
                using var regedit = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Applets\Regedit", writable: false);
                var val = regedit?.GetValue("LastKey") as string ?? string.Empty;
                if (val.StartsWith("Computer\\", StringComparison.OrdinalIgnoreCase))
                    return "Computer\\";
            }
            catch { }
            // Default to no Computer\ prefix for compatibility
            return string.Empty;
        }

        private static string NormalizeHive(string hive)
        {
            hive = hive.Trim();
            if (hive.Equals("HKLM", StringComparison.OrdinalIgnoreCase)) return "HKEY_LOCAL_MACHINE";
            if (hive.Equals("HKCU", StringComparison.OrdinalIgnoreCase)) return "HKEY_CURRENT_USER";
            if (hive.Equals("HKEY_LOCAL_MACHINE", StringComparison.OrdinalIgnoreCase)) return "HKEY_LOCAL_MACHINE";
            if (hive.Equals("HKEY_CURRENT_USER", StringComparison.OrdinalIgnoreCase)) return "HKEY_CURRENT_USER";
            return hive;
        }

        private static string NormalizeKey(string key)
        {
            key = key.Replace('/', '\\').Trim();
            if (key.StartsWith("\\")) key = key.TrimStart('\\');
            return key;
        }

        private static string GetDeepestExistingSubKey(string hiveName, string subPath)
        {
            try
            {
                using var baseKey = hiveName.Equals("HKEY_LOCAL_MACHINE", StringComparison.OrdinalIgnoreCase)
                    ? RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Default)
                    : RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default);

                if (string.IsNullOrEmpty(subPath))
                    return string.Empty;

                var parts = subPath.Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);
                string deepest = string.Empty;
                RegistryKey? current = baseKey;

                foreach (var p in parts)
                {
                    if (current == null) break;
                    var next = current.OpenSubKey(p);
                    if (next == null) break;
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
