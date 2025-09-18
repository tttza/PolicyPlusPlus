using System;
using System.IO;
using System.Linq;
using PolicyPlusCore.Admx;
using PolicyPlusCore.Core;
using PolicyPlusPlus.ViewModels;
using PolicyPlusPlus.Services;

namespace SharedTest;

public static class AdmxTestHelper
{
    public static string ResolveAdmxAssetsPath()
    {
        try
        {
            var env = Environment.GetEnvironmentVariable("POLICYPLUS_TEST_ADMX_DIR");
            if (!string.IsNullOrWhiteSpace(env) && Directory.Exists(env) && Directory.EnumerateFiles(env, "*.admx", SearchOption.TopDirectoryOnly).Any())
                return env;
        }
        catch { }

        var baseDir = AppContext.BaseDirectory;
        string[] localCandidates =
        {
            Path.Combine(baseDir, "TestAssets", "Admx"),
            Path.Combine(baseDir, "Admx"),
        };
        foreach (var c in localCandidates)
        {
            try { if (Directory.Exists(c) && Directory.EnumerateFiles(c, "*.admx", SearchOption.TopDirectoryOnly).Any()) return c; } catch { }
        }
        try
        {
            var dirInfo = new DirectoryInfo(baseDir);
            for (int i = 0; i < 12 && dirInfo != null; i++)
            {
                var shared = Path.Combine(dirInfo.FullName, "TestAssets", "Admx");
                if (Directory.Exists(shared) && Directory.EnumerateFiles(shared, "*.admx").Any()) return shared;
                var legacy = Path.Combine(dirInfo.FullName, "PolicyPlusPlus.Tests.UI", "TestAssets", "Admx");
                if (Directory.Exists(legacy) && Directory.EnumerateFiles(legacy, "*.admx").Any()) return legacy;
                dirInfo = dirInfo.Parent;
            }
        }
        catch { }
        throw new DirectoryNotFoundException("ADMX test assets folder not found from base directory: " + baseDir);
    }

    public static AdmxBundle LoadBundle(out string path, string language = "en-US")
    {
        path = ResolveAdmxAssetsPath();
        var b = new AdmxBundle();
        foreach (var _ in b.LoadFolder(path, language)) { }
        return b;
    }


    public static void ClearPending()
    { try { PendingChangesService.Instance.Pending.Clear(); } catch { } }
}
