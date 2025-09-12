// Separated elevation host implementation
using Microsoft.Win32;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization; // for JsonPropertyName

namespace PolicyPPElevationHost
{
    internal sealed class HostResponse
    {
        [JsonPropertyName("ok")] public bool Ok { get; set; }
        [JsonPropertyName("error")] public string? Error { get; set; }
    }

    internal static class ElevationHost
    {
        [DllImport("userenv.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
        private static extern bool RefreshPolicyEx(bool IsMachine, uint Options);

        private const uint RP_FORCE = 0x1;
        private static volatile bool s_logEnabled = false;
        private static string? s_clientSid;
        private static string? s_authToken;
        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);
        private static string HostLogPath => Path.Combine(Path.GetTempPath(), "PolicyPlus_host.log");

        private static void Log(string msg)
        {
            if (!s_logEnabled)
                return;
            try
            {
                File.AppendAllText(
                    HostLogPath,
                    DateTime.Now.ToString("s") + "[" + Environment.ProcessId + "] " + msg + Environment.NewLine
                );
            }
            catch
            {
            }
        }

        public static int Run(string pipeName)
        {
            try
            {
                var cmd = Environment.GetCommandLineArgs();
                for (int i = 0; i < cmd.Length; i++)
                {
                    if (string.Equals(cmd[i], "--client-sid", StringComparison.OrdinalIgnoreCase) && i + 1 < cmd.Length)
                        s_clientSid = cmd[i + 1];
                    if (string.Equals(cmd[i], "--auth", StringComparison.OrdinalIgnoreCase) && i + 1 < cmd.Length)
                        s_authToken = cmd[i + 1];
                    if (string.Equals(cmd[i], "--log", StringComparison.OrdinalIgnoreCase))
                        s_logEnabled = true;
                }
            }
            catch
            {
            }

            if (string.IsNullOrEmpty(s_clientSid) || string.IsNullOrEmpty(s_authToken))
            {
                Log("Missing client-sid or auth");
                return 1;
            }

            Log("Host starting. Pipe=" + pipeName + ", clientSid=" + s_clientSid);
            try
            {
                var ps = new PipeSecurity();
                try
                {
                    var sid = new SecurityIdentifier(s_clientSid);
                    ps.AddAccessRule(new PipeAccessRule(
                        sid,
                        PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance | PipeAccessRights.Synchronize,
                        AccessControlType.Allow));

                    var me = WindowsIdentity.GetCurrent();
                    if (me?.User != null)
                    {
                        ps.AddAccessRule(new PipeAccessRule(
                            me.User,
                            PipeAccessRights.FullControl,
                            AccessControlType.Allow));
                    }
                }
                catch (Exception ex)
                {
                    Log("PipeSecurity setup failed: " + ex.Message);
                    return 1;
                }

                using var server = NamedPipeServerStreamAcl.Create(
                    pipeName,
                    PipeDirection.InOut,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous,
                    4096,
                    4096,
                    ps);

                Log("Server created, waiting for client...");
                server.WaitForConnection();
                Log("Client connected.");

                using var reader = new StreamReader(server, Encoding.UTF8, false, 4096, leaveOpen: true);
                using var writer = new StreamWriter(server, Encoding.UTF8, 4096, leaveOpen: true)
                {
                    AutoFlush = true,
                    NewLine = "\n"
                };

                const int MaxChars = 12 * 1024 * 1024;
                while (true)
                {
                    string? line = reader.ReadLine();
                    if (line == null)
                    {
                        Log("Client disconnected.");
                        break;
                    }

                    if (line.Length > MaxChars)
                    {
                        try
                        {
                            writer.WriteLine(JsonSerializer.Serialize(new HostResponse
                            {
                                Ok = false,
                                Error = "request too large"
                            }));
                        }
                        catch
                        {
                        }
                        continue;
                    }

                    try
                    {
                        using var doc = JsonDocument.Parse(line);
                        var root = doc.RootElement;
                        var auth = root.TryGetProperty("auth", out var a) ? a.GetString() : null;
                        if (!string.Equals(auth, s_authToken, StringComparison.Ordinal))
                        {
                            Log("Auth failed");
                            writer.WriteLine("{\"ok\":false,\"error\":\"unauthorized\"}");
                            break;
                        }

                        var op = root.GetProperty("op").GetString();
                        if (string.Equals(op, "write-local-gpo", StringComparison.OrdinalIgnoreCase))
                        {
                            string? machineBytes = root.TryGetProperty("machineBytes", out var mb) ? mb.GetString() : null;
                            string? userBytes = root.TryGetProperty("userBytes", out var ub) ? ub.GetString() : null;
                            bool refresh = root.TryGetProperty("refresh", out var r) && r.GetBoolean();
                            var (ok, error) = WriteLocalGpo(machineBytes, userBytes);
                            var resp = new HostResponse { Ok = ok, Error = error };
                            writer.WriteLine(JsonSerializer.Serialize(resp));

                            if (ok && refresh)
                            {
                                Task.Run(async () =>
                                {
                                    try
                                    {
                                        RefreshPolicyEx(true, RP_FORCE);
                                        Log("RefreshPolicyEx(machine, FORCE) invoked");
                                    }
                                    catch (Exception ex)
                                    {
                                        Log("RefreshPolicyEx(machine) failed: " + ex.Message);
                                    }

                                    try
                                    {
                                        await Task.Delay(100).ConfigureAwait(false);
                                    }
                                    catch
                                    {
                                    }

                                    try
                                    {
                                        RefreshPolicyEx(false, RP_FORCE);
                                        Log("RefreshPolicyEx(user, FORCE) invoked");
                                    }
                                    catch (Exception ex)
                                    {
                                        Log("RefreshPolicyEx(user) failed: " + ex.Message);
                                    }
                                });
                            }
                        }
                        else if (string.Equals(op, "open-regedit", StringComparison.OrdinalIgnoreCase))
                        {
                            string hive = root.TryGetProperty("hive", out var hv) ? hv.GetString() ?? string.Empty : string.Empty;
                            string key = root.TryGetProperty("subKey", out var sk) ? sk.GetString() ?? string.Empty : string.Empty;
                            var (ok, error) = OpenRegeditAt(hive, key);
                            writer.WriteLine(JsonSerializer.Serialize(new HostResponse { Ok = ok, Error = error }));
                        }
                        else if (string.Equals(op, "shutdown", StringComparison.OrdinalIgnoreCase))
                        {
                            writer.WriteLine(JsonSerializer.Serialize(new HostResponse { Ok = true }));
                            break;
                        }
                        else
                        {
                            writer.WriteLine(JsonSerializer.Serialize(new HostResponse
                            {
                                Ok = false,
                                Error = "unknown op"
                            }));
                        }
                    }
                    catch (Exception ex)
                    {
                        try
                        {
                            writer.WriteLine(JsonSerializer.Serialize(new HostResponse
                            {
                                Ok = false,
                                Error = ex.ToString()
                            }));
                        }
                        catch
                        {
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log("Fatal host error: " + ex);
                return 1;
            }

            Log("Host exiting.");
            return 0;
        }

        private static bool IsPolBytes(byte[] bytes) =>
            bytes != null &&
            bytes.Length >= 8 &&
            bytes[0] == 'P' &&
            bytes[1] == 'R' &&
            bytes[2] == 'e' &&
            bytes[3] == 'g';

        private static (bool ok, string? error) WriteLocalGpo(string? machineBytes, string? userBytes)
        {
            try
            {
                string windir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                string gpRoot = Path.Combine(windir, "System32", "GroupPolicy");
                string machinePol = Path.Combine(gpRoot, "Machine", "Registry.pol");
                string userPol = Path.Combine(gpRoot, "User", "Registry.pol");

                Directory.CreateDirectory(Path.Combine(gpRoot, "Machine"));
                Directory.CreateDirectory(Path.Combine(gpRoot, "User"));

                bool wroteMachine = false;
                bool wroteUser = false;

                if (!string.IsNullOrEmpty(machineBytes))
                {
                    var bytes = Convert.FromBase64String(machineBytes);
                    if (!IsPolBytes(bytes))
                        return (false, "invalid machine pol bytes");

                    using var fs = new FileStream(machinePol, FileMode.Create, FileAccess.Write, FileShare.None);
                    fs.Write(bytes, 0, bytes.Length);
                    fs.Flush(true);
                    wroteMachine = true;
                }

                if (!string.IsNullOrEmpty(userBytes))
                {
                    var bytes = Convert.FromBase64String(userBytes);
                    if (!IsPolBytes(bytes))
                        return (false, "invalid user pol bytes");

                    using var fs = new FileStream(userPol, FileMode.Create, FileAccess.Write, FileShare.None);
                    fs.Write(bytes, 0, bytes.Length);
                    fs.Flush(true);
                    wroteUser = true;
                }

                var gptIni = Path.Combine(gpRoot, "gpt.ini");
                UpdateGptIni(gptIni, wroteMachine, wroteUser);
                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, ex.ToString());
            }
        }

        private static void UpdateGptIni(string gptIniPath, bool bumpMachine, bool bumpUser)
        {
            const string MachExtensionsLine = "gPCMachineExtensionNames=[{35378EAC-683F-11D2-A89A-00C04FBBCFA2}{D02B1F72-3407-48AE-BA88-E8213C6761F1}]";
            const string UserExtensionsLine = "gPCUserExtensionNames=[{35378EAC-683F-11D2-A89A-00C04FBBCFA2}{D02B1F73-3407-48AE-BA88-E8213C6761F1}]";

            var dir = Path.GetDirectoryName(gptIniPath) ?? string.Empty;
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            if (File.Exists(gptIniPath))
            {
                var lines = File.ReadAllLines(gptIniPath);
                bool seenMachExts = false;
                bool seenUserExts = false;
                bool seenVersion = false;
                int lo = 0;
                int hi = 0;

                foreach (var line in lines)
                {
                    if (line.StartsWith("Version", StringComparison.InvariantCultureIgnoreCase))
                    {
                        int curVersion = 0;
                        var parts = line.Split('=');
                        if (parts.Length == 2)
                            int.TryParse(parts[1], out curVersion);
                        lo = curVersion & 0xFFFF;
                        hi = (curVersion >> 16) & 0xFFFF;
                        break;
                    }
                }

                if (bumpMachine)
                    lo = (lo + 1) & 0xFFFF;
                if (bumpUser)
                    hi = (hi + 1) & 0xFFFF;

                using var f = new StreamWriter(gptIniPath, false, Utf8NoBom);
                foreach (var line in lines)
                {
                    if (line.StartsWith("Version", StringComparison.InvariantCultureIgnoreCase))
                    {
                        int newVersion = (hi << 16) | (lo & 0xFFFF);
                        f.WriteLine("Version=" + newVersion);
                        seenVersion = true;
                    }
                    else
                    {
                        f.WriteLine(line);
                        if (line.StartsWith("gPCMachineExtensionNames=", StringComparison.InvariantCultureIgnoreCase))
                            seenMachExts = true;
                        if (line.StartsWith("gPCUserExtensionNames=", StringComparison.InvariantCultureIgnoreCase))
                            seenUserExts = true;
                    }
                }

                if (!seenVersion)
                {
                    int newVersion = (hi << 16) | (lo & 0xFFFF);
                    f.WriteLine("Version=" + newVersion);
                }
                if (!seenMachExts)
                    f.WriteLine(MachExtensionsLine);
                if (!seenUserExts)
                    f.WriteLine(UserExtensionsLine);
            }
            else
            {
                using var f = new StreamWriter(gptIniPath, false, Utf8NoBom);
                f.WriteLine("[General]");
                f.WriteLine(MachExtensionsLine);
                f.WriteLine(UserExtensionsLine);
                int lo = bumpMachine ? 1 : 0;
                int hi = bumpUser ? 1 : 0;
                int version = (hi << 16) | (lo & 0xFFFF);
                if (version == 0)
                    version = 0x10001; // fallback baseline version when nothing bumped
                f.WriteLine("Version=" + version);
            }
        }

        private static (bool ok, string? error) OpenRegeditAt(string hive, string subKey)
        {
            try
            {
                string hiveName = NormalizeHive(hive);
                string normalizedSub = NormalizeKey(subKey ?? string.Empty);
                string existing = GetDeepestExistingSubKey(hiveName, normalizedSub);
                string targetPath = string.IsNullOrEmpty(existing) ? hiveName : (hiveName + "\\" + existing);

                using var regedit = Registry.CurrentUser.CreateSubKey(@"Software\\Microsoft\\Windows\\CurrentVersion\\Applets\\Regedit");
                if (regedit == null)
                    return (false, "regedit key unavailable");

                regedit.SetValue("DisableLastKey", 0, RegistryValueKind.DWord);
                string prefix = TryGetCurrentLastKeyPrefixStyle(regedit);
                regedit.SetValue("LastKey", prefix + targetPath, RegistryValueKind.String);

                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "regedit.exe",
                    Arguments = "/m",
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        private static string TryGetCurrentLastKeyPrefixStyle(RegistryKey regedit)
        {
            try
            {
                var val = regedit.GetValue("LastKey") as string ?? string.Empty;
                if (val.StartsWith("Computer\\", StringComparison.OrdinalIgnoreCase))
                    return "Computer\\";
            }
            catch
            {
            }
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
            key = (key ?? string.Empty)
                .Replace('/', '\\')
                .Trim();
            if (key.StartsWith("\\"))
                key = key.TrimStart('\\');
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
                    if (current == null)
                        break;
                    var next = current.OpenSubKey(p);
                    if (next == null)
                        break;
                    deepest = string.IsNullOrEmpty(deepest) ? p : (deepest + "\\" + p);
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
