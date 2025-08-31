using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Win32;
using System.Security.AccessControl;
using System.Security.Principal;

namespace PolicyPlus.WinUI3
{
    internal static class ElevationHost
    {
        [DllImport("userenv.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
        private static extern bool RefreshPolicyEx(bool IsMachine, uint Options);

        private static volatile bool s_logEnabled = false;

        private static string HostLogPath
        {
            get
            {
                try
                {
                    var dir = Path.GetTempPath();
                    return Path.Combine(dir, "PolicyPlus_host.log");
                }
                catch { return "C:\\PolicyPlus_host.log"; }
            }
        }
        private static void Log(string msg)
        {
            if (!s_logEnabled) return;
            try { File.AppendAllText(HostLogPath, DateTime.Now.ToString("s") + " [" + Environment.ProcessId + "] " + msg + Environment.NewLine); } catch { }
        }

        public static int Run(string pipeName)
        {
            // Determine logging preference
            try
            {
                var cmd = Environment.GetCommandLineArgs();
                foreach (var a in cmd) { if (string.Equals(a, "--log", StringComparison.OrdinalIgnoreCase)) { s_logEnabled = true; break; } }
                if (!s_logEnabled)
                {
                    var ev = Environment.GetEnvironmentVariable("POLICYPLUS_HOST_LOG");
                    s_logEnabled = string.Equals(ev, "1", StringComparison.OrdinalIgnoreCase) || string.Equals(ev, "true", StringComparison.OrdinalIgnoreCase);
                }
            }
            catch { }

            Log("Host starting. Pipe=" + pipeName);
            try
            {
                var ps = new PipeSecurity();
                try
                {
                    ps.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null), PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance | PipeAccessRights.Synchronize, AccessControlType.Allow));
                    var me = WindowsIdentity.GetCurrent();
                    if (me?.User != null)
                        ps.AddAccessRule(new PipeAccessRule(me.User, PipeAccessRights.FullControl, AccessControlType.Allow));
                }
                catch (Exception ex) { Log("PipeSecurity setup failed: " + ex.Message); }

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
                using var writer = new StreamWriter(server, Encoding.UTF8, 4096, leaveOpen: true) { AutoFlush = true, NewLine = "\n" };

                while (true)
                {
                    string? line = reader.ReadLine();
                    if (line == null) { Log("Client disconnected."); break; }
                    try
                    {
                        Log("Received: " + (line.Length > 400 ? line.Substring(0, 400) + "..." : line));
                        using var doc = JsonDocument.Parse(line);
                        var root = doc.RootElement;
                        var op = root.GetProperty("op").GetString();
                        if (string.Equals(op, "write-local-gpo", StringComparison.OrdinalIgnoreCase))
                        {
                            string? machineBytes = root.TryGetProperty("machineBytes", out var mb) ? mb.GetString() : null;
                            string? userBytes = root.TryGetProperty("userBytes", out var ub) ? ub.GetString() : null;
                            bool refresh = root.TryGetProperty("refresh", out var r) && r.GetBoolean();
                            Log($"write-local-gpo machineBytes={(machineBytes?.Length ?? 0)} userBytes={(userBytes?.Length ?? 0)} refresh={refresh}");
                            var (ok, error) = WriteLocalGpo(machineBytes, userBytes);
                            Log($"write result ok={ok} err={(error ?? "")} ");
                            writer.WriteLine(JsonSerializer.Serialize(new { ok, error }));
                            if (ok && refresh)
                            {
                                Task.Run(() =>
                                {
                                    try { RefreshPolicyEx(true, 0); Log("RefreshPolicyEx(machine) invoked"); } catch (Exception ex) { Log("RefreshPolicyEx(machine) failed: " + ex.Message); }
                                    try { RefreshPolicyEx(false, 0); Log("RefreshPolicyEx(user) invoked"); } catch (Exception ex) { Log("RefreshPolicyEx(user) failed: " + ex.Message); }
                                });
                            }
                        }
                        else if (string.Equals(op, "shutdown", StringComparison.OrdinalIgnoreCase))
                        {
                            Log("Shutdown requested");
                            writer.WriteLine("{\"ok\":true}");
                            break;
                        }
                        else
                        {
                            Log("Unknown op: " + op);
                            writer.WriteLine("{\"ok\":false,\"error\":\"unknown op\"}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log("Error handling request: " + ex);
                        try { writer.WriteLine(JsonSerializer.Serialize(new { ok = false, error = ex.ToString() })); } catch { }
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

                if (!string.IsNullOrEmpty(machineBytes))
                {
                    var bytes = Convert.FromBase64String(machineBytes);
                    Log($"Writing machine pol to {machinePol}, {bytes.Length} bytes");
                    using var fs = new FileStream(machinePol, FileMode.Create, FileAccess.Write, FileShare.None);
                    fs.Write(bytes, 0, bytes.Length);
                    fs.Flush(true);
                }
                if (!string.IsNullOrEmpty(userBytes))
                {
                    var bytes = Convert.FromBase64String(userBytes);
                    Log($"Writing user pol to {userPol}, {bytes.Length} bytes");
                    using var fs = new FileStream(userPol, FileMode.Create, FileAccess.Write, FileShare.None);
                    fs.Write(bytes, 0, bytes.Length);
                    fs.Flush(true);
                }

                var gptIni = Path.Combine(gpRoot, "gpt.ini");
                UpdateGptIni(gptIni);
                Log("Updated gpt.ini at " + gptIni);

                return (true, null);
            }
            catch (Exception ex)
            {
                Log("WriteLocalGpo failed: " + ex);
                return (false, ex.ToString());
            }
        }

        private static void UpdateGptIni(string gptIniPath)
        {
            const string MachExtensionsLine = "gPCMachineExtensionNames=[{35378EAC-683F-11D2-A89A-00C04FBBCFA2}{D02B1F72-3407-48AE-BA88-E8213C6761F1}]";
            const string UserExtensionsLine = "gPCUserExtensionNames=[{35378EAC-683F-11D2-A89A-00C04FBBCFA2}{D02B1F73-3407-48AE-BA88-E8213C6761F1}]";
            var dir = Path.GetDirectoryName(gptIniPath) ?? string.Empty;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            if (File.Exists(gptIniPath))
            {
                var lines = File.ReadAllLines(gptIniPath);
                bool seenMachExts = false, seenUserExts = false, seenVersion = false;
                using var f = new StreamWriter(gptIniPath, false, Encoding.UTF8);
                foreach (var line in lines)
                {
                    if (line.StartsWith("Version", StringComparison.InvariantCultureIgnoreCase))
                    {
                        int curVersion = 0;
                        var parts = line.Split('=');
                        if (parts.Length == 2) int.TryParse(parts[1], out curVersion);
                        curVersion += 0x10001;
                        f.WriteLine("Version=" + curVersion);
                        seenVersion = true;
                    }
                    else
                    {
                        f.WriteLine(line);
                        if (line.StartsWith("gPCMachineExtensionNames=", StringComparison.InvariantCultureIgnoreCase)) seenMachExts = true;
                        if (line.StartsWith("gPCUserExtensionNames=", StringComparison.InvariantCultureIgnoreCase)) seenUserExts = true;
                    }
                }
                if (!seenVersion) f.WriteLine("Version=" + 0x10001);
                if (!seenMachExts) f.WriteLine(MachExtensionsLine);
                if (!seenUserExts) f.WriteLine(UserExtensionsLine);
            }
            else
            {
                using var f = new StreamWriter(gptIniPath, false, Encoding.UTF8);
                f.WriteLine("[General]");
                f.WriteLine(MachExtensionsLine);
                f.WriteLine(UserExtensionsLine);
                f.WriteLine("Version=" + 0x10001);
            }
        }
    }
}
