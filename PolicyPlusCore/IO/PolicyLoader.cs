using System.Runtime.InteropServices;
using System.Security.Principal; // elevation check
using Microsoft.Win32;
using PolicyPlusCore.Helpers;
using PolicyPlusCore.Utils;

namespace PolicyPlusCore.IO
{
    public class PolicyLoader
    {
        private PolicyLoaderSource SourceType;
        private string OriginalArgument;
        private bool User; // Whether this is for a user policy source
        private IPolicySource SourceObject = new PolFile();
        private string MainSourcePath = string.Empty; // Path to the POL file or NTUSER.DAT
        private RegistryKey? MainSourceRegKey; // The hive key, or the mounted hive file
        private string GptIniPath = string.Empty; // Path to the gpt.ini file, used to increment the version
        private bool Writable;
        private readonly bool _testMemoryPol =
            Environment.GetEnvironmentVariable("POLICYPLUS_TEST_MEMORY_POL") == "1";
        private readonly string? _testDataDir = Environment.GetEnvironmentVariable(
            "POLICYPLUS_TEST_DATA_DIR"
        );

        public PolicyLoader(PolicyLoaderSource Source, string Argument, bool IsUser)
        {
            SourceType = Source;
            User = IsUser;
            OriginalArgument = Argument;
            // Parse the argument and open the physical resource
            switch (Source)
            {
                case PolicyLoaderSource.LocalGpo:
                {
                    if (_testMemoryPol)
                    {
                        // In test mode we avoid touching real system GroupPolicy directory. Use temp path if persistence needed.
                        if (!string.IsNullOrWhiteSpace(_testDataDir))
                        {
                            var kind = IsUser ? "User" : "Machine";
                            MainSourcePath = Path.Combine(_testDataDir, kind + ".pol");
                            GptIniPath = Path.Combine(_testDataDir, "gpt.ini");
                        }
                        else
                        {
                            MainSourcePath = string.Empty; // memory only
                        }
                    }
                    else
                    {
                        MainSourcePath = Environment.ExpandEnvironmentVariables(
                            @"%SYSTEMROOT%\\System32\\GroupPolicy\\"
                                + (IsUser ? "User" : "Machine")
                                + @"\\Registry.pol"
                        );
                        GptIniPath = Environment.ExpandEnvironmentVariables(
                            @"%SYSTEMROOT%\\System32\\GroupPolicy\\gpt.ini"
                        );
                    }
                    break; // prevent fall-through
                }

                case PolicyLoaderSource.LocalRegistry:
                {
                    var pathParts = Argument.Split(new[] { '\\' }, 2);
                    string baseName = pathParts[0].ToLowerInvariant();
                    RegistryKey baseKey;
                    if (baseName == "hkcu" | baseName == "hkey_current_user")
                    {
                        baseKey = RegistryKey.OpenBaseKey(
                            RegistryHive.CurrentUser,
                            RegistryView.Default
                        );
                    }
                    else if (baseName == "hku" | baseName == "hkey_users")
                    {
                        baseKey = RegistryKey.OpenBaseKey(RegistryHive.Users, RegistryView.Default);
                    }
                    else if (baseName == "hklm" | baseName == "hkey_local_machine")
                    {
                        baseKey = RegistryKey.OpenBaseKey(
                            RegistryHive.LocalMachine,
                            RegistryView.Default
                        );
                    }
                    else
                    {
                        throw new Exception("The root key is not valid.");
                    }

                    if (pathParts.Length == 2)
                    {
                        MainSourceRegKey = baseKey.CreateSubKey(pathParts[1]);
                    }
                    else
                    {
                        MainSourceRegKey = baseKey;
                    }

                    break;
                }

                case PolicyLoaderSource.PolFile:
                {
                    MainSourcePath = Argument;
                    break;
                }

                case PolicyLoaderSource.SidGpo:
                {
                    MainSourcePath = Environment.ExpandEnvironmentVariables(
                        @"%SYSTEMROOT%\\System32\\GroupPolicyUsers\\"
                            + Argument
                            + @"\\User\\Registry.pol"
                    );
                    GptIniPath = Environment.ExpandEnvironmentVariables(
                        @"%SYSTEMROOT%\\System32\\GroupPolicyUsers\\" + Argument + @"\\gpt.ini"
                    );
                    break;
                }

                case PolicyLoaderSource.NtUserDat:
                {
                    MainSourcePath = Argument;
                    break;
                }

                case PolicyLoaderSource.Null:
                {
                    MainSourcePath = "";
                    break;
                }
            }
        }

        public IPolicySource OpenSource()
        {
            // Create an IPolicySource so PolicyProcessing can work
            switch (SourceType)
            {
                case PolicyLoaderSource.LocalGpo when _testMemoryPol:
                {
                    // Pure in-memory or temp-backed PolFile without hitting real LGPO.
                    if (string.IsNullOrEmpty(MainSourcePath))
                    {
                        SourceObject = new PolFile();
                        Writable = true;
                    }
                    else
                    {
                        try
                        {
                            if (File.Exists(MainSourcePath))
                                SourceObject = PolFile.Load(MainSourcePath);
                            else
                            {
                                Directory.CreateDirectory(Path.GetDirectoryName(MainSourcePath)!);
                                var pf = new PolFile();
                                pf.Save(MainSourcePath);
                                SourceObject = pf;
                            }
                            Writable = true;
                        }
                        catch
                        {
                            SourceObject = new PolFile();
                            Writable = true;
                        }
                    }
                    return SourceObject;
                }
                case PolicyLoaderSource.LocalRegistry:
                {
                    if (MainSourceRegKey is null)
                    {
                        Writable = false;
                        SourceObject = new PolFile();
                        break;
                    }
                    var regPol = RegistryPolicyProxy.EncapsulateKey(MainSourceRegKey);
                    try
                    {
                        regPol.SetValue(
                            @"Software\\Policies",
                            "_PolicyPlusSecCheck",
                            "Testing to see whether Policy Plus can write to policy keys",
                            RegistryValueKind.String
                        );
                        regPol.DeleteValue(@"Software\\Policies", "_PolicyPlusSecCheck");
                        Writable = true;
                    }
                    catch (Exception)
                    {
                        Writable = false;
                    }

                    SourceObject = regPol;
                    break;
                }

                case PolicyLoaderSource.NtUserDat:
                {
                    // Turn on the backup and restore privileges to allow the use of RegLoadKey
                    Privilege.EnablePrivilege("SeBackupPrivilege");
                    Privilege.EnablePrivilege("SeRestorePrivilege");
                    // Load the hive
                    using (
                        var machHive = RegistryKey.OpenBaseKey(
                            RegistryHive.LocalMachine,
                            RegistryView.Default
                        )
                    )
                    {
                        string subkeyName = "PolicyPlusMount:" + Guid.NewGuid().ToString();
                        PInvoke.RegLoadKeyW(
                            new nint(int.MinValue + 0x00000002),
                            subkeyName,
                            MainSourcePath
                        ); // HKEY_LOCAL_MACHINE
                        MainSourceRegKey = machHive.OpenSubKey(subkeyName, true);
                        if (MainSourceRegKey is null)
                        {
                            Writable = false;
                            SourceObject = new PolFile();
                            return SourceObject;
                        }
                        else
                        {
                            Writable = true;
                        }
                    }

                    SourceObject = RegistryPolicyProxy.EncapsulateKey(MainSourceRegKey);
                    break;
                }

                case PolicyLoaderSource.Null:
                {
                    SourceObject = new PolFile();
                    break;
                }

                default:
                {
                    if (File.Exists(MainSourcePath))
                    {
                        // For LocalGpo / SidGpo when not elevated, skip write probe to avoid UnauthorizedAccessException spam.
                        bool skipWriteProbe =
                            (
                                SourceType == PolicyLoaderSource.LocalGpo
                                || SourceType == PolicyLoaderSource.SidGpo
                            ) && !IsProcessElevated();
                        if (!skipWriteProbe)
                        {
                            try
                            {
                                using (
                                    var fPol = new FileStream(
                                        MainSourcePath,
                                        FileMode.Open,
                                        FileAccess.ReadWrite
                                    )
                                )
                                {
                                    Writable = true;
                                }
                            }
                            catch (Exception)
                            {
                                Writable = false;
                            }
                        }
                        else
                        {
                            Writable = false; // explicitly mark non-writable when not elevated
                            try
                            {
                                // Open read-only once to ensure file is at least accessible
                                using var _ = new FileStream(
                                    MainSourcePath,
                                    FileMode.Open,
                                    FileAccess.Read,
                                    FileShare.Read
                                );
                            }
                            catch (Exception)
                            {
                                // If read also fails fall back to empty PolFile below.
                            }
                        }

                        SourceObject = PolFile.Load(MainSourcePath);
                    }
                    else
                    {
                        // Create a new POL file
                        try
                        {
                            var dir = Path.GetDirectoryName(MainSourcePath);
                            if (!string.IsNullOrEmpty(dir))
                                Directory.CreateDirectory(dir);
                            var pol = new PolFile();
                            pol.Save(MainSourcePath);
                            SourceObject = pol;
                            Writable = true;
                        }
                        catch (Exception)
                        {
                            SourceObject = new PolFile();
                            Writable = false;
                        }
                    }

                    break;
                }
            }

            return SourceObject;
        }

        public bool Close() // Whether cleanup was successful
        {
            if (SourceType == PolicyLoaderSource.NtUserDat & SourceObject is RegistryPolicyProxy)
            {
                string subkeyName = MainSourceRegKey!.Name.Split(new[] { '\\' }, 2)[1]; // Remove the host hive name
                MainSourceRegKey!.Dispose();
                return PInvoke.RegUnLoadKeyW(new nint(int.MinValue + 0x00000002), subkeyName) == 0;
            }

            return true;
        }

        public string Save() // Returns human-readable info on what happened
        {
            switch (SourceType)
            {
                case PolicyLoaderSource.LocalGpo when _testMemoryPol:
                {
                    // In test mode just persist to temp file if path provided; no system refresh.
                    if (!string.IsNullOrEmpty(MainSourcePath) && SourceObject is PolFile pf)
                    {
                        try
                        {
                            pf.Save(MainSourcePath);
                        }
                        catch { }
                        return "saved to temp";
                    }
                    return "memory only";
                }
                case PolicyLoaderSource.LocalGpo:
                {
                    PolFile oldPol;
                    if (File.Exists(MainSourcePath))
                        oldPol = PolFile.Load(MainSourcePath);
                    else
                        oldPol = new PolFile();
                    PolFile pol = (PolFile)SourceObject;
                    pol.Save(MainSourcePath);
                    UpdateGptIni();
                    // Figure out whether this edition can handle Group Policy application by itself
                    if (SystemInfo.HasGroupPolicyInfrastructure())
                    {
                        // Core path intentionally skips DC reachability checks; user-facing warnings are handled in the elevation host path.
                        bool refreshed = PInvoke.RefreshPolicyEx(!User, 0U);
                        if (!refreshed)
                        {
                            int err = Marshal.GetLastWin32Error();
                            return $"saved to disk but policy refresh failed (error=0x{err:X})";
                        }
                        return "saved to disk and invoked policy refresh";
                    }
                    else
                    {
                        pol.ApplyDifference(
                            oldPol,
                            RegistryPolicyProxy.EncapsulateKey(
                                User ? RegistryHive.CurrentUser : RegistryHive.LocalMachine
                            )
                        );
                        PInvoke.SendNotifyMessageW(new nint(0xFFFF), 0x1A, nuint.Zero, nint.Zero); // Broadcast WM_SETTINGCHANGE
                        return "saved to disk and applied diff to Registry";
                    }
                }

                case PolicyLoaderSource.LocalRegistry:
                {
                    return "already applied";
                }

                case PolicyLoaderSource.NtUserDat:
                {
                    return "will apply when policy source is closed";
                }

                case PolicyLoaderSource.Null:
                {
                    return "discarded";
                }

                case PolicyLoaderSource.PolFile:
                {
                    ((PolFile)SourceObject).Save(MainSourcePath);
                    return "saved to disk";
                }

                case PolicyLoaderSource.SidGpo:
                {
                    ((PolFile)SourceObject).Save(MainSourcePath);
                    UpdateGptIni();
                    bool refreshed = PInvoke.RefreshPolicyEx(false, 0U);
                    if (!refreshed)
                    {
                        int err = Marshal.GetLastWin32Error();
                        return $"saved to disk but policy refresh failed (error=0x{err:X})";
                    }
                    return "saved to disk and invoked policy refresh";
                }
            }

            return "";
        }

        public string GetCmtxPath()
        {
            // Get the path to the comments file, or nothing if comments don't work
            if (
                SourceType == PolicyLoaderSource.PolFile
                | SourceType == PolicyLoaderSource.NtUserDat
            )
            {
                return Path.ChangeExtension(MainSourcePath, "cmtx");
            }
            else if (SourceType == PolicyLoaderSource.LocalRegistry)
            {
                return Environment.ExpandEnvironmentVariables(
                    @"%LOCALAPPDATA%\\Policy Plus\\Reg" + (User ? "User" : "Machine") + ".cmtx"
                );
            }
            else if (!string.IsNullOrEmpty(MainSourcePath))
            {
                var dir = Path.GetDirectoryName(MainSourcePath);
                return string.IsNullOrEmpty(dir) ? string.Empty : Path.Combine(dir, "comment.cmtx");
            }
            else
            {
                return "";
            }
        }

        public PolicySourceWritability GetWritability()
        {
            // Get whether the source can be updated
            if (SourceType == PolicyLoaderSource.Null)
            {
                return PolicySourceWritability.Writable;
            }
            else if (SourceType == PolicyLoaderSource.LocalRegistry)
            {
                return Writable
                    ? PolicySourceWritability.Writable
                    : PolicySourceWritability.NoWriting;
            }
            else
            {
                return Writable
                    ? PolicySourceWritability.Writable
                    : PolicySourceWritability.NoCommit;
            }
        }

        private void UpdateGptIni()
        {
            // Increment the version number in gpt.ini
            const string MachExtensionsLine =
                "gPCMachineExtensionNames=[{35378EAC-683F-11D2-A89A-00C04FBBCFA2}{D02B1F72-3407-48AE-BA88-E8213C6761F1}]";
            const string UserExtensionsLine =
                "gPCUserExtensionNames=[{35378EAC-683F-11D2-A89A-00C04FBBCFA2}{D02B1F73-3407-48AE-BA88-E8213C6761F1}]";
            if (File.Exists(GptIniPath))
            {
                // Alter the existing gpt.ini's Version line and add any necessary other lines
                var lines = File.ReadLines(GptIniPath).ToList();
                using (var fGpt = new StreamWriter(GptIniPath, false))
                {
                    bool seenMachExts = default,
                        seenUserExts = default,
                        seenVersion = default;
                    foreach (var line in lines)
                    {
                        if (line.StartsWith("Version", StringComparison.InvariantCultureIgnoreCase))
                        {
                            int curVersion = int.Parse(line.Split(new[] { '=' }, 2)[1]);
                            curVersion += User ? 0x10000 : 1;
                            fGpt.WriteLine("Version=" + curVersion);
                            seenVersion = true;
                        }
                        else
                        {
                            fGpt.WriteLine(line);
                            if (
                                line.StartsWith(
                                    "gPCMachineExtensionNames=",
                                    StringComparison.InvariantCultureIgnoreCase
                                )
                            )
                                seenMachExts = true;
                            if (
                                line.StartsWith(
                                    "gPCUserExtensionNames=",
                                    StringComparison.InvariantCultureIgnoreCase
                                )
                            )
                                seenUserExts = true;
                        }
                    }

                    if (!seenVersion)
                        fGpt.WriteLine("Version=" + 0x10001);
                    if (!seenMachExts)
                        fGpt.WriteLine(MachExtensionsLine);
                    if (!seenUserExts)
                        fGpt.WriteLine(UserExtensionsLine);
                }
            }
            else
            {
                // Create a new gpt.ini
                using (var fGpt = new StreamWriter(GptIniPath))
                {
                    fGpt.WriteLine("[General]");
                    fGpt.WriteLine(MachExtensionsLine);
                    fGpt.WriteLine(UserExtensionsLine);
                    fGpt.WriteLine("Version=" + 0x10001);
                }
            }
        }

        public PolicyLoaderSource Source => SourceType;

        public string LoaderData => OriginalArgument;

        public string GetDisplayInfo()
        {
            // Get the human-readable name of the loader for display in the status bar
            string name = "";
            switch (SourceType)
            {
                case PolicyLoaderSource.LocalGpo:
                {
                    name = "Local GPO";
                    break;
                }

                case PolicyLoaderSource.LocalRegistry:
                {
                    name = "Registry";
                    break;
                }

                case PolicyLoaderSource.PolFile:
                {
                    name = "File";
                    break;
                }

                case PolicyLoaderSource.SidGpo:
                {
                    name = "User GPO";
                    break;
                }

                case PolicyLoaderSource.NtUserDat:
                {
                    name = "User hive";
                    break;
                }

                case PolicyLoaderSource.Null:
                {
                    name = "Scratch space";
                    break;
                }
            }

            if (!string.IsNullOrEmpty(OriginalArgument))
                return name + " (" + OriginalArgument + ")";
            else
                return name;
        }

        private static bool IsProcessElevated()
        {
            try
            {
                using var id = WindowsIdentity.GetCurrent();
                var pr = new WindowsPrincipal(id);
                return pr.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }
    }

    public enum PolicyLoaderSource
    {
        LocalGpo,
        LocalRegistry,
        PolFile,
        SidGpo,
        NtUserDat,
        Null,
    }

    public enum PolicySourceWritability
    {
        Writable, // Full writability
        NoCommit, // Enable the OK button, but don't try to save
        NoWriting, // Disable the OK button (there's no buffer)
    }
}
