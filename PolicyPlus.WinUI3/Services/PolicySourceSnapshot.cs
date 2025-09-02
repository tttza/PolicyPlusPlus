using Microsoft.Win32;
using System;

namespace PolicyPlus.WinUI3.Services
{
    public static class PolicySourceSnapshot
    {
        public static PolFile SnapshotLocalPolicyToPol(bool isUser)
        {
            var pol = new PolFile();
            try
            {
                var hive = isUser ? RegistryHive.CurrentUser : RegistryHive.LocalMachine;
                using var root = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
                foreach (var policyRoot in RegistryPolicyProxy.PolicyKeys)
                {
                    try
                    {
                        using var key = root.OpenSubKey(policyRoot, false);
                        if (key == null) continue;
                        CopyKeyRecursive(key, policyRoot, pol);
                    }
                    catch { }
                }
            }
            catch { }
            return pol;
        }

        public static RegFile SnapshotAllPolicyToReg()
        {
            var reg = new RegFile();
            reg.SetPrefix(string.Empty);
            try
            {
                SnapshotHiveToReg(RegistryHive.LocalMachine, "HKEY_LOCAL_MACHINE", reg);
            }
            catch { }
            try
            {
                SnapshotHiveToReg(RegistryHive.CurrentUser, "HKEY_CURRENT_USER", reg);
            }
            catch { }
            return reg;
        }

        private static void SnapshotHiveToReg(RegistryHive hive, string hiveName, RegFile reg)
        {
            using var root = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
            foreach (var policyRoot in RegistryPolicyProxy.PolicyKeys)
            {
                try
                {
                    using var key = root.OpenSubKey(policyRoot, false);
                    if (key == null) continue;
                    CopyKeyRecursiveToReg(key, hiveName + "\\" + policyRoot, reg);
                }
                catch { }
            }
        }

        private static void CopyKeyRecursive(RegistryKey key, string path, PolFile pol)
        {
            try
            {
                foreach (var name in key.GetValueNames())
                {
                    try
                    {
                        var kind = key.GetValueKind(name);
                        var data = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                        switch (kind)
                        {
                            case RegistryValueKind.DWord:
                                pol.SetValue(path, name, Convert.ToUInt32(data ?? 0), RegistryValueKind.DWord);
                                break;
                            case RegistryValueKind.QWord:
                                pol.SetValue(path, name, Convert.ToUInt64(data ?? 0UL), RegistryValueKind.QWord);
                                break;
                            case RegistryValueKind.MultiString:
                                pol.SetValue(path, name, (data as string[]) ?? Array.Empty<string>(), RegistryValueKind.MultiString);
                                break;
                            default:
                                pol.SetValue(path, name, data ?? string.Empty, kind);
                                break;
                        }
                    }
                    catch { }
                }

                foreach (var sub in key.GetSubKeyNames())
                {
                    try
                    {
                        using var child = key.OpenSubKey(sub, false);
                        if (child != null)
                        {
                            CopyKeyRecursive(child, path + "\\" + sub, pol);
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        private static void CopyKeyRecursiveToReg(RegistryKey key, string fullPath, RegFile reg)
        {
            try
            {
                foreach (var name in key.GetValueNames())
                {
                    try
                    {
                        var kind = key.GetValueKind(name);
                        var data = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                        switch (kind)
                        {
                            case RegistryValueKind.DWord:
                                reg.SetValue(fullPath, name, Convert.ToUInt32(data ?? 0), RegistryValueKind.DWord);
                                break;
                            case RegistryValueKind.QWord:
                                reg.SetValue(fullPath, name, Convert.ToUInt64(data ?? 0UL), RegistryValueKind.QWord);
                                break;
                            case RegistryValueKind.MultiString:
                                reg.SetValue(fullPath, name, (data as string[]) ?? Array.Empty<string>(), RegistryValueKind.MultiString);
                                break;
                            default:
                                reg.SetValue(fullPath, name, data ?? string.Empty, kind);
                                break;
                        }
                    }
                    catch { }
                }

                foreach (var sub in key.GetSubKeyNames())
                {
                    try
                    {
                        using var child = key.OpenSubKey(sub, false);
                        if (child != null)
                        {
                            CopyKeyRecursiveToReg(child, fullPath + "\\" + sub, reg);
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }
    }
}
