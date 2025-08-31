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
using System.IO.Pipes; // Acl helper

namespace PolicyPlus.WinUI3
{
    internal static class ElevationHost
    {
        [DllImport("userenv.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
        private static extern bool RefreshPolicyEx(bool IsMachine, uint Options);

        private static volatile bool s_logEnabled = false;
        private static string? s_clientSid;
        private static string? s_authToken;

        private static string HostLogPath => Path.Combine(Path.GetTempPath(), "PolicyPlus_host.log");
        private static void Log(string msg)
        { if (!s_logEnabled) return; try { File.AppendAllText(HostLogPath, DateTime.Now.ToString("s") + " [" + Environment.ProcessId + "] " + msg + Environment.NewLine); } catch { } }

        public static int Run(string pipeName)
        {
            try
            {
                var cmd = Environment.GetCommandLineArgs();
                for (int i = 0; i < cmd.Length; i++)
                {
                    if (string.Equals(cmd[i], "--client-sid", StringComparison.OrdinalIgnoreCase) && i + 1 < cmd.Length) s_clientSid = cmd[i + 1];
                    if (string.Equals(cmd[i], "--auth", StringComparison.OrdinalIgnoreCase) && i + 1 < cmd.Length) s_authToken = cmd[i + 1];
                    if (string.Equals(cmd[i], "--log", StringComparison.OrdinalIgnoreCase)) s_logEnabled = true;
                }
            }
            catch { }

            if (string.IsNullOrEmpty(s_clientSid) || string.IsNullOrEmpty(s_authToken))
            { Log("Missing client-sid or auth"); return 1; }

            Log("Host starting. Pipe=" + pipeName + ", clientSid=" + s_clientSid);
            try
            {
                var ps = new PipeSecurity();
                try
                {
                    var sid = new SecurityIdentifier(s_clientSid);
                    ps.AddAccessRule(new PipeAccessRule(sid, PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance | PipeAccessRights.Synchronize, AccessControlType.Allow));
                    var me = WindowsIdentity.GetCurrent();
                    if (me?.User != null) ps.AddAccessRule(new PipeAccessRule(me.User, PipeAccessRights.FullControl, AccessControlType.Allow));
                }
                catch (Exception ex) { Log("PipeSecurity setup failed: " + ex.Message); return 1; }

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
                    string? line = ReadLineLimited(reader, 8 * 1024 * 1024);
                    if (line == null) { Log("Client disconnected."); break; }
                    try
                    {
                        using var doc = JsonDocument.Parse(line);
                        var root = doc.RootElement;
                        var auth = root.TryGetProperty("auth", out var a) ? a.GetString() : null;
                        if (!string.Equals(auth, s_authToken, StringComparison.Ordinal))
                        { Log("Auth failed"); writer.WriteLine("{\"ok\":false,\"error\":\"unauthorized\"}"); break; }

                        var op = root.GetProperty("op").GetString();
                        if (string.Equals(op, "write-local-gpo", StringComparison.OrdinalIgnoreCase))
                        {
                            string? machineBytes = root.TryGetProperty("machineBytes", out var mb) ? mb.GetString() : null;
                            string? userBytes = root.TryGetProperty("userBytes", out var ub) ? ub.GetString() : null;
                            bool refresh = root.TryGetProperty("refresh", out var r) && r.GetBoolean();
                            var (ok, error) = WriteLocalGpo(machineBytes, userBytes);
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
                            writer.WriteLine("{\"ok\":true}");
                            break;
                        }
                        else
                        {
                            writer.WriteLine("{\"ok\":false,\"error\":\"unknown op\"}");
                        }
                    }
                    catch (Exception ex)
                    {
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

        private static string? ReadLineLimited(StreamReader reader, int maxBytes)
        {
            var ms = new MemoryStream();
            while (true)
            {
                int ch = reader.Read();
                if (ch < 0) return null;
                if (ch == '\n') break;
                ms.WriteByte((byte)ch);
                if (ms.Length > maxBytes) throw new IOException("request too large");
            }
            return Encoding.UTF8.GetString(ms.ToArray());
        }

        private static bool IsPolBytes(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 8) return false;
            return bytes[0] == (byte)'P' && bytes[1] == (byte)'R' && bytes[2] == (byte)'e' && bytes[3] == (byte)'g';
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
                    if (!IsPolBytes(bytes)) return (false, "invalid machine pol bytes");
                    using var fs = new FileStream(machinePol, FileMode.Create, FileAccess.Write, FileShare.None);
                    fs.Write(bytes, 0, bytes.Length);
                    fs.Flush(true);
                }
                if (!string.IsNullOrEmpty(userBytes))
                {
                    var bytes = Convert.FromBase64String(userBytes);
                    if (!IsPolBytes(bytes)) return (false, "invalid user pol bytes");
                    using var fs = new FileStream(userPol, FileMode.Create, FileAccess.Write, FileShare.None);
                    fs.Write(bytes, 0, bytes.Length);
                    fs.Flush(true);
                }

                var gptIni = Path.Combine(gpRoot, "gpt.ini");
                UpdateGptIni(gptIni);
                return (true, null);
            }
            catch (Exception ex)
            { return (false, ex.ToString()); }
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
