using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using PolicyPlusPlus.Logging;

namespace PolicyPlusPlus.Services
{
    internal static class CacheService
    {
        private static string EnsureDir()
        {
            try
            {
                SettingsService.Instance.Initialize();
            }
            catch { }
            var dir = SettingsService.Instance.CacheDirectory;
            try
            {
                Directory.CreateDirectory(dir);
            }
            catch { }
            return dir;
        }

        private static string Hash(string s)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(s ?? string.Empty));
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes)
                sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        public static string ComputeAdmxFingerprint(string admxRoot, string? language)
        {
            try
            {
                var sb = new StringBuilder();
                if (Directory.Exists(admxRoot))
                {
                    var admxFiles = Directory
                        .EnumerateFiles(admxRoot, "*.admx", SearchOption.TopDirectoryOnly)
                        .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    foreach (var f in admxFiles)
                    {
                        var fi = new FileInfo(f);
                        sb.Append(Path.GetFileName(f));
                        sb.Append('|');
                        sb.Append(fi.Length);
                        sb.Append('|');
                        sb.Append(fi.LastWriteTimeUtc.Ticks);
                        sb.Append(';');
                    }

                    if (!string.IsNullOrEmpty(language))
                    {
                        var admlDir = Path.Combine(admxRoot, language);
                        if (Directory.Exists(admlDir))
                        {
                            var admlFiles = Directory
                                .EnumerateFiles(admlDir, "*.adml", SearchOption.TopDirectoryOnly)
                                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                                .ToList();
                            foreach (var f in admlFiles)
                            {
                                var fi = new FileInfo(f);
                                sb.Append(language);
                                sb.Append('/');
                                sb.Append(Path.GetFileName(f));
                                sb.Append('|');
                                sb.Append(fi.Length);
                                sb.Append('|');
                                sb.Append(fi.LastWriteTimeUtc.Ticks);
                                sb.Append(';');
                            }
                        }
                    }
                }
                return Hash(sb.ToString());
            }
            catch
            {
                return ""; // empty when fingerprinting fails
            }
        }

        // ADMX warm snapshot (UI-facing DTO) save/load for skipping XML parses.
        public static bool TryLoadAdmxWarmSnapshot(
            string admxPath,
            string language,
            string fingerprint,
            out PolicyPlusCore.Core.AdmxWarmSnapshot? snapshot
        )
        {
            snapshot = null;
            try
            {
                if (string.IsNullOrWhiteSpace(admxPath) || string.IsNullOrWhiteSpace(language))
                    return false;
                var key =
                    $"admxwarm_{Hash(admxPath + "\n" + language + "\n" + fingerprint)}.json.gz";
                var path = Path.Combine(EnsureDir(), key);
                if (!File.Exists(path))
                {
                    try
                    {
                        Log.Debug("Cache", $"Warm snapshot miss path={path}");
                    }
                    catch { }
                    return false;
                }
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var gz = new GZipStream(fs, CompressionMode.Decompress, leaveOpen: false);
                using var ms = new MemoryStream();
                gz.CopyTo(ms);
                var json = Encoding.UTF8.GetString(ms.ToArray());
                snapshot =
                    System.Text.Json.JsonSerializer.Deserialize<PolicyPlusCore.Core.AdmxWarmSnapshot>(
                        json
                    );
                bool ok = snapshot != null;
                try
                {
                    Log.Debug("Cache", $"Warm snapshot load {(ok ? "hit" : "fail")} path={path}");
                }
                catch { }
                return ok;
            }
            catch
            {
                snapshot = null;
                try
                {
                    Log.Debug("Cache", "Warm snapshot load threw");
                }
                catch { }
                return false;
            }
        }

        public static void SaveAdmxWarmSnapshot(
            string admxPath,
            string language,
            string fingerprint,
            PolicyPlusCore.Core.AdmxWarmSnapshot snapshot
        )
        {
            try
            {
                var key =
                    $"admxwarm_{Hash(admxPath + "\n" + language + "\n" + fingerprint)}.json.gz";
                var path = Path.Combine(EnsureDir(), key);
                var json = System.Text.Json.JsonSerializer.Serialize(snapshot);
                using var fs = new FileStream(
                    path,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.Read
                );
                using var gz = new GZipStream(fs, CompressionLevel.Fastest, leaveOpen: false);
                var bytes = Encoding.UTF8.GetBytes(json);
                gz.Write(bytes, 0, bytes.Length);
                try
                {
                    Log.Info("Cache", $"Warm snapshot saved path={path} size={bytes.Length}");
                }
                catch { }
            }
            catch (Exception ex)
            {
                try
                {
                    Log.Warn("Cache", $"Warm snapshot save failed lang={language}", ex);
                }
                catch { }
            }
        }
    }
}
