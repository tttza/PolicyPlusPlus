using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using AppAutomation = FlaUI.Core.Application;

namespace PolicyPlusPlus.Tests.UI.Infrastructure;

public sealed class TestAppHost : IDisposable
{
    private readonly string _exePath;
    private readonly string? _forcedTestDataDir; // optional externally supplied test data directory
    private Process? _process;
    public AppAutomation? App { get; private set; }
    public AutomationBase? Automation { get; private set; }
    public Window? MainWindow { get; private set; }
    private readonly StringBuilder _stdout = new();
    private readonly StringBuilder _stderr = new();
    private string? _testDataDirectory; // actual directory used (forced or generated)
    public string TestDataDirectory => _testDataDirectory ?? string.Empty; // exposed for tests to read settings.json

    public TestAppHost() : this(null) { }

    public TestAppHost(string? testDataDirectory)
    {
        _forcedTestDataDir = testDataDirectory;
        var solutionDir = FindSolutionDirectory();
        var appProjectDir = Path.Combine(solutionDir, "PolicyPlusPlus");
        var baseConfig = GetConfiguration();
        var arch = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.X86 => "x86",
            Architecture.Arm64 => "ARM64",
            _ => "x64"
        };
        var configs = new[] { baseConfig, ToggleUnpackaged(baseConfig), "Debug-Unpackaged", "Debug", "Release-Unpackaged", "Release" }
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        _exePath = ResolveExecutablePath(appProjectDir, configs, arch)
            ?? throw new FileNotFoundException("App executable not found. Ensure PolicyPlusPlus project builds before UI tests.");
    }

    private static string ToggleUnpackaged(string cfg)
        => cfg.EndsWith("-Unpackaged", StringComparison.OrdinalIgnoreCase) ? cfg.Replace("-Unpackaged", string.Empty) : (cfg + "-Unpackaged");

    private static string? ResolveExecutablePath(string appProjectDir, string[] configs, string arch)
    {
        var binDir = Path.Combine(appProjectDir, "bin");
        if (!Directory.Exists(binDir)) return null;
        foreach (var cfg in configs)
        {
            string[] bases =
            {
                Path.Combine(binDir, arch, cfg),
                Path.Combine(binDir, cfg, arch),
                Path.Combine(binDir, cfg)
            };
            foreach (var b in bases)
            {
                if (!Directory.Exists(b)) continue;
                try
                {
                    foreach (var tfm in Directory.EnumerateDirectories(b, "net8.0-windows*").OrderByDescending(d => d))
                    {
                        var exe = Path.Combine(tfm, "PolicyPlusPlus.exe");
                        if (File.Exists(exe)) return exe;
                    }
                    var directExe = Path.Combine(b, "PolicyPlusPlus.exe");
                    if (File.Exists(directExe)) return directExe;
                }
                catch { }
            }
        }
        try
        {
            var matches = Directory.EnumerateFiles(binDir, "PolicyPlusPlus.exe", SearchOption.AllDirectories).Take(50).ToList();
            if (matches.Count > 0)
            {
                return matches
                    .OrderByDescending(p => p.Contains("Unpackaged", StringComparison.OrdinalIgnoreCase))
                    .ThenByDescending(p => p.Contains("Debug", StringComparison.OrdinalIgnoreCase))
                    .ThenByDescending(p => p)
                    .First();
            }
        }
        catch { }
        return null;
    }

    public void Launch()
    {
        if (App != null) return;
        _testDataDirectory = _forcedTestDataDir ?? Path.Combine(Path.GetTempPath(), "PolicyPlusUITest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDataDirectory);
        var psi = new ProcessStartInfo(_exePath)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = Path.GetDirectoryName(_exePath)!
        };
        psi.Environment["POLICYPLUS_TEST_DATA_DIR"] = _testDataDirectory;
        psi.Environment["POLICYPLUS_TEST_MEMORY_POL"] = "1";
        try
        {
            var solutionDir = FindSolutionDirectory();
            var dummyDir = Path.Combine(solutionDir, "PolicyPlusPlus.Tests.UI", "TestAssets", "Admx");
            if (Directory.Exists(dummyDir))
            {
                psi.Environment["POLICYPLUS_TEST_ADMX_DIR"] = dummyDir;
            }
        }
        catch { }
        psi.Environment["POLICYPLUS_FORCE_LANGUAGE"] = "en-US";
        psi.Environment["POLICYPLUS_FORCE_SECOND_LANGUAGE"] = "fr-FR";
        psi.Environment["POLICYPLUS_FORCE_SECOND_ENABLED"] = "1";
        _process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start application");
        try
        {
            _process.OutputDataReceived += (_, e) => { if (e.Data != null) _stdout.AppendLine(e.Data); };
            _process.ErrorDataReceived += (_, e) => { if (e.Data != null) _stderr.AppendLine(e.Data); };
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();
        }
        catch { }

        // Attach using process Id to avoid accessing Process.MainModule which can throw / be null in some runners
        try
        {
            App = AppAutomation.Attach(_process.Id);
        }
        catch
        {
            Thread.Sleep(500);
            App = AppAutomation.Attach(_process.Id); // second attempt
        }

        Automation = new UIA3Automation();

        var timeout = TimeSpan.FromSeconds(60);
        var start = DateTime.UtcNow;
        Exception? lastEx = null;
        while ((DateTime.UtcNow - start) < timeout)
        {
            try
            {
                var wins = App.GetAllTopLevelWindows(Automation);
                MainWindow = wins.FirstOrDefault(IsTargetWindow);
                if (MainWindow != null) break;
                var native = NativeFindCandidateWindow(_process.Id);
                if (native != IntPtr.Zero)
                {
                    try
                    {
                        var autoEl = Automation.FromHandle(native);
                        MainWindow = new Window(autoEl.FrameworkAutomationElement);
                        if (IsTargetWindow(MainWindow)) break;
                    }
                    catch { }
                }
            }
            catch (Exception ex) { lastEx = ex; }
            if (_process.HasExited)
            {
                throw new InvalidOperationException("Application exited early. ExitCode=" + _process.ExitCode + BuildDiagnostics());
            }
            Thread.Sleep(500);
        }
        if (MainWindow == null)
        {
            var diag = BuildDiagnostics(listWindows: true);
            throw new TimeoutException("Main window not found" + diag + (lastEx != null ? " Last exception: " + lastEx.Message : string.Empty));
        }
    }

    private bool IsTargetWindow(Window w)
    {
        try
        {
            if (w is null) return false;
            var title = w.Title ?? string.Empty;
            return title.Contains("Policy", StringComparison.OrdinalIgnoreCase) || title.Contains("Policy++", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    private static IntPtr NativeFindCandidateWindow(int pid)
    {
        var matches = new List<IntPtr>();
        EnumWindows((h, l) =>
        {
            if (!IsWindowVisible(h)) return true;
            _ = GetWindowThreadProcessId(h, out var wpid);
            if (wpid == pid) matches.Add(h);
            return true;
        }, IntPtr.Zero);
        foreach (var h in matches)
        {
            var title = GetWindowTextSafe(h);
            if (!string.IsNullOrWhiteSpace(title)) return h;
        }
        return matches.FirstOrDefault();
    }

    private static string GetWindowTextSafe(IntPtr h)
    {
        var sb = new StringBuilder(512);
        if (GetWindowText(h, sb, sb.Capacity) > 0) return sb.ToString();
        return string.Empty;
    }

    private string BuildDiagnostics(bool listWindows = false)
    {
        var sb = new StringBuilder();
        try
        {
            sb.AppendLine();
            sb.AppendLine("[Diagnostics]");
            sb.AppendLine("Path=" + _exePath);
            if (_process != null) sb.AppendLine("ProcessId=" + _process.Id + " HasExited=" + _process.HasExited);
            if (listWindows && Automation != null && App != null)
            {
                try
                {
                    var wins = App.GetAllTopLevelWindows(Automation);
                    sb.AppendLine("EnumeratedWindows=" + wins.Length);
                    foreach (var w in wins)
                    {
                        sb.AppendLine(" - Title='" + w.Title + "' IsModal=" + w.IsModal + " Bounds=" + w.BoundingRectangle);
                    }
                }
                catch { }
            }
            sb.AppendLine("StdOut:");
            sb.AppendLine(_stdout.ToString());
            sb.AppendLine("StdErr:");
            sb.AppendLine(_stderr.ToString());
        }
        catch { }
        return sb.ToString();
    }

    private static string GetConfiguration()
    {
        var cfg = Environment.GetEnvironmentVariable("CONFIGURATION");
        return string.IsNullOrWhiteSpace(cfg) ? "Debug" : cfg;
    }

    private static string FindSolutionDirectory()
    {
        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            if (File.Exists(Path.Combine(dir, "PolicyPlusMod.sln"))) return dir;
            dir = Directory.GetParent(dir)?.FullName ?? string.Empty;
        }
        throw new DirectoryNotFoundException("Solution directory not found");
    }

    public void Dispose()
    {
        try
        {
            Automation?.Dispose();
            if (_process != null && !_process.HasExited)
            {
                _process.Kill(true);
                _process.WaitForExit(5000);
            }
        }
        catch { }
    }

    #region Native
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll", SetLastError = true)] private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    [DllImport("user32.dll", SetLastError = true)] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);
    #endregion
}
