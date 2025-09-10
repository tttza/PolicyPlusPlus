using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Principal;
using System.Security.Cryptography;
using PolicyPlus.WinUI3.Serialization;
using PolicyPlus.WinUI3.Logging; // logging

namespace PolicyPlus.WinUI3.Services
{
    internal sealed class ElevationService
    {
        private static readonly Lazy<ElevationService> _lazy = new(() => new ElevationService());
        public static ElevationService Instance => _lazy.Value;

        private string? _pipeName;
        private NamedPipeClientStream? _client;
        private StreamReader? _reader;
        private StreamWriter? _writer;
        private bool _connected;
        private readonly SemaphoreSlim _ioLock = new(1, 1);
        private Process? _hostProc;
        private string? _clientSid;
        private string? _authToken;

        private ElevationService() { }

        private static string ClientLogPath => Path.Combine(Path.GetTempPath(), "PolicyPlus_client.log");
        private static void ClientLog(string msg)
        {
            if (!IsHostLoggingEnabled()) return;
            try { File.AppendAllText(ClientLogPath, DateTime.Now.ToString("s") + "[" + Environment.ProcessId + "] " + msg + Environment.NewLine); } catch { }
        }

        private string GetCurrentExePath()
        {
            try { return Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule!.FileName; } catch { return Process.GetCurrentProcess().MainModule!.FileName; }
        }

        private static bool IsHostLoggingEnabled()
        {
            try
            {
                var ev = Environment.GetEnvironmentVariable("POLICYPLUS_HOST_LOG");
                if (string.Equals(ev, "1", StringComparison.OrdinalIgnoreCase) || string.Equals(ev, "true", StringComparison.OrdinalIgnoreCase))
                    return true;
                var cmd = Environment.GetCommandLineArgs();
                for (int i = 0; i < cmd.Length; i++)
                {
                    if (string.Equals(cmd[i], "--log", StringComparison.OrdinalIgnoreCase)) return true;
                }
            }
            catch { }
            return false;
        }

        private async Task<(bool ok, ElevationErrorCode code, Exception? error)> EnsureHostAsync()
        {
            Debug.Assert(_ioLock.CurrentCount == 0, "EnsureHostAsync requires caller to hold _ioLock to avoid races during connection initialization.");
            if (_connected && _client != null && _client.IsConnected) return (true, ElevationErrorCode.None, null);

            _pipeName = ("PolicyPlusElevate-" + Guid.NewGuid().ToString("N"));
            _clientSid = WindowsIdentity.GetCurrent()?.User?.Value;
            if (string.IsNullOrEmpty(_clientSid)) return (false, ElevationErrorCode.Unknown, new InvalidOperationException("Cannot determine caller SID"));
            _authToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

            var args = new StringBuilder();
            args.Append("--elevation-host ").Append(_pipeName);
            args.Append(" --client-sid \"").Append(_clientSid.Replace("\"", "")).Append("\"");
            args.Append(" --auth ").Append(_authToken);
            if (IsHostLoggingEnabled()) args.Append(" --log");

            var psi = new ProcessStartInfo
            {
                FileName = GetCurrentExePath(),
                UseShellExecute = true,
                Verb = "runas",
                Arguments = args.ToString(),
                WindowStyle = ProcessWindowStyle.Hidden,
                WorkingDirectory = AppContext.BaseDirectory
            };

            ClientLog("Starting elevated host: " + psi.Arguments);
            try { _hostProc = Process.Start(psi); }
            catch (Exception ex) { ClientLog("Start failed: " + ex.Message); return (false, ElevationErrorCode.StartFailed, ex); }

            _client = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

            var sw = Stopwatch.StartNew();
            Exception? last = null;
            while (sw.Elapsed < TimeSpan.FromSeconds(10))
            {
                try
                {
                    await _client.ConnectAsync(500).ConfigureAwait(false);
                    break;
                }
                catch (Exception ex)
                {
                    last = ex;
                    try { if (_hostProc != null && _hostProc.HasExited) { ClientLog("Host exited early. Code=" + _hostProc.ExitCode); return (false, ElevationErrorCode.HostExited, ex); } } catch { }
                    await Task.Delay(100).ConfigureAwait(false);
                }
            }
            if (_client == null || !_client.IsConnected)
            {
                ClientLog("Pipe connect failed: " + last?.Message);
                var code = last is UnauthorizedAccessException ? ElevationErrorCode.Unauthorized : ElevationErrorCode.ConnectFailed;
                return (false, code, last);
            }

            _reader = new StreamReader(_client, Encoding.UTF8, false, 4096, leaveOpen: true);
            _writer = new StreamWriter(_client, Encoding.UTF8, 4096, leaveOpen: true) { AutoFlush = true, NewLine = "\n" };
            _connected = true;
            return (true, ElevationErrorCode.None, null);
        }

        private static ElevationResult ClassifyResponse(bool ok, string? err)
        {
            if (ok) return ElevationResult.Success;
            if (string.IsNullOrEmpty(err)) return ElevationResult.FromError(ElevationErrorCode.Unknown, null);
            var lower = err.ToLowerInvariant();
            if (lower.Contains("unauthorized")) return ElevationResult.FromError(ElevationErrorCode.Unauthorized, err);
            if (lower.Contains("timeout")) return ElevationResult.FromError(ElevationErrorCode.Timeout, err);
            if (lower.Contains("io") || lower.Contains("pipe")) return ElevationResult.FromError(ElevationErrorCode.IoError, err);
            return ElevationResult.FromError(ElevationErrorCode.Unknown, err);
        }

        private async Task<ElevationResult> ExecuteWithRetryAsync(Func<Task<ElevationResult>> action, int maxAttempts = 3, int initialDelayMs = 150)
        {
            ElevationResult last = ElevationResult.FromError(ElevationErrorCode.Unknown, "uninitialized");
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    last = await action().ConfigureAwait(false);
                    if (last.Ok) return last;
                    // Only retry for transient connect / IO errors
                    if (last.Code is not ElevationErrorCode.NotConnected and not ElevationErrorCode.ConnectFailed and not ElevationErrorCode.IoError and not ElevationErrorCode.HostExited)
                        return last;
                }
                catch (Exception ex)
                {
                    last = ElevationResult.FromError(ElevationErrorCode.IoError, ex.Message);
                }
                if (attempt < maxAttempts)
                {
                    await Task.Delay(initialDelayMs * attempt).ConfigureAwait(false); // simple linear backoff
                    // Force re-connect next attempt
                    _connected = false;
                    try { _reader?.Dispose(); } catch { }
                    try { _writer?.Dispose(); } catch { }
                    try { _client?.Dispose(); } catch { }
                    _reader = null; _writer = null; _client = null;
                }
            }
            return last;
        }

        public async Task<ElevationResult> WriteLocalGpoBytesAsync(string? machinePolBase64, string? userPolBase64, bool triggerRefresh = true)
        {
            return await ExecuteWithRetryAsync(async () =>
            {
                await _ioLock.WaitAsync().ConfigureAwait(false);
                try
                {
                    var ensured = await EnsureHostAsync().ConfigureAwait(false);
                    if (!ensured.ok)
                    {
                        return ElevationResult.FromError(ensured.code == ElevationErrorCode.None ? ElevationErrorCode.ConnectFailed : ensured.code, ensured.error?.Message);
                    }
                    if (_writer == null || _reader == null) return ElevationResult.FromError(ElevationErrorCode.NotConnected, "not connected");

                    var payload = new HostRequestWriteLocalGpo
                    {
                        Auth = _authToken,
                        MachinePol = null,
                        UserPol = null,
                        MachineBytes = machinePolBase64,
                        UserBytes = userPolBase64,
                        Refresh = triggerRefresh
                    };
                    var json = JsonSerializer.Serialize(payload, AppJsonContext.Default.HostRequestWriteLocalGpo);
                    await _writer.WriteLineAsync(json).ConfigureAwait(false);

                    var readTask = _reader.ReadLineAsync();
                    var timeoutTask = Task.Delay(8000);
                    var completed = await Task.WhenAny(readTask, timeoutTask).ConfigureAwait(false);
                    if (completed == timeoutTask) return ElevationResult.FromError(ElevationErrorCode.Timeout, "elevated host timeout");
                    string? resp = await readTask.ConfigureAwait(false);
                    if (string.IsNullOrEmpty(resp)) return ElevationResult.FromError(ElevationErrorCode.ProtocolError, "no response");
                    try
                    {
                        using var doc = JsonDocument.Parse(resp);
                        var root = doc.RootElement;
                        bool ok = root.TryGetProperty("ok", out var okProp) && okProp.GetBoolean();
                        string? err = root.TryGetProperty("error", out var errProp) ? errProp.GetString() : null;
                        return ClassifyResponse(ok, err);
                    }
                    catch (Exception ex)
                    {
                        return ElevationResult.FromError(ElevationErrorCode.ProtocolError, ex.Message);
                    }
                }
                finally
                {
                    _ioLock.Release();
                }
            }).ConfigureAwait(false);
        }

        public async Task<ElevationResult> WriteLocalGpoAsync(string? machinePolTempPath, string? userPolTempPath, bool triggerRefresh = true)
        {
            string? machineBytes = null;
            string? userBytes = null;
            try
            {
                if (!string.IsNullOrEmpty(machinePolTempPath) && File.Exists(machinePolTempPath))
                    machineBytes = Convert.ToBase64String(await File.ReadAllBytesAsync(machinePolTempPath).ConfigureAwait(false));
            }
            catch (Exception ex)
            {
                Log.Warn("Elevation", $"read temp machine POL failed path={machinePolTempPath}", ex);
            }
            try
            {
                if (!string.IsNullOrEmpty(userPolTempPath) && File.Exists(userPolTempPath))
                    userBytes = Convert.ToBase64String(await File.ReadAllBytesAsync(userPolTempPath).ConfigureAwait(false));
            }
            catch (Exception ex)
            {
                Log.Warn("Elevation", $"read temp user POL failed path={userPolTempPath}", ex);
            }
            return await WriteLocalGpoBytesAsync(machineBytes, userBytes, triggerRefresh).ConfigureAwait(false);
        }

        public async Task<ElevationResult> OpenRegeditAtAsync(string hive, string subKey)
        {
            return await ExecuteWithRetryAsync(async () =>
            {
                await _ioLock.WaitAsync().ConfigureAwait(false);
                try
                {
                    var ensured = await EnsureHostAsync().ConfigureAwait(false);
                    if (!ensured.ok)
                    {
                        return ElevationResult.FromError(ensured.code == ElevationErrorCode.None ? ElevationErrorCode.ConnectFailed : ensured.code, ensured.error?.Message);
                    }
                    if (_writer == null || _reader == null) return ElevationResult.FromError(ElevationErrorCode.NotConnected, "not connected");

                    var payload = new HostRequestOpenRegedit { Auth = _authToken, Hive = hive, SubKey = subKey };
                    var json = JsonSerializer.Serialize(payload, AppJsonContext.Default.HostRequestOpenRegedit);
                    await _writer.WriteLineAsync(json).ConfigureAwait(false);

                    var readTask = _reader.ReadLineAsync();
                    var timeoutTask = Task.Delay(8000);
                    var completed = await Task.WhenAny(readTask, timeoutTask).ConfigureAwait(false);
                    if (completed == timeoutTask) return ElevationResult.FromError(ElevationErrorCode.Timeout, "elevated host timeout");
                    string? resp = await readTask.ConfigureAwait(false);
                    if (string.IsNullOrEmpty(resp)) return ElevationResult.FromError(ElevationErrorCode.ProtocolError, "no response");
                    try
                    {
                        using var doc = JsonDocument.Parse(resp);
                        var root = doc.RootElement;
                        bool ok = root.TryGetProperty("ok", out var okProp) && okProp.GetBoolean();
                        string? err = root.TryGetProperty("error", out var errProp) ? errProp.GetString() : null;
                        return ClassifyResponse(ok, err);
                    }
                    catch (Exception ex)
                    {
                        return ElevationResult.FromError(ElevationErrorCode.ProtocolError, ex.Message);
                    }
                }
                finally
                {
                    _ioLock.Release();
                }
            }).ConfigureAwait(false);
        }

        public async Task ShutdownAsync()
        {
            await _ioLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_connected && _writer != null && _reader != null)
                {
                    var req = new HostRequestShutdown { Auth = _authToken };
                    var json = JsonSerializer.Serialize(req, AppJsonContext.Default.HostRequestShutdown);
                    try { await _writer.WriteLineAsync(json).ConfigureAwait(false); } catch (Exception ex) { Log.Warn("Elevation", "shutdown write failed", ex); }
                    try { await _writer.FlushAsync().ConfigureAwait(false); } catch (Exception ex) { Log.Warn("Elevation", "shutdown flush failed", ex); }
                    try { await Task.Delay(200).ConfigureAwait(false); } catch (Exception ex) { Log.Warn("Elevation", "shutdown delay failed", ex); }
                }
            }
            finally
            {
                try { _reader?.Dispose(); } catch (Exception ex) { Log.Warn("Elevation", "reader dispose failed", ex); }
                try { _writer?.Dispose(); } catch (Exception ex) { Log.Warn("Elevation", "writer dispose failed", ex); }
                try { _client?.Dispose(); } catch (Exception ex) { Log.Warn("Elevation", "client dispose failed", ex); }
                _reader = null; _writer = null; _client = null; _connected = false;
                _ioLock.Release();
                try
                {
                    if (_hostProc != null && !_hostProc.HasExited)
                    {
                        _hostProc.Kill(true);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warn("Elevation", "kill host failed", ex);
                }
                _hostProc = null;
            }
        }
    }
}
