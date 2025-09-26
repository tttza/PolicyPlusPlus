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
        private const string LogArea = "CacheSvc"; // Service-level operations creating cache directory

        private static string EnsureDir()
        {
            try
            {
                SettingsService.Instance.Initialize();
            }
            catch (Exception ex)
            {
                // Non-fatal; settings init failure already likely logged elsewhere, but record here for correlation.
                Logging.Log.Debug(LogArea, "Settings initialize failed: " + ex.Message);
            }
            var dir = SettingsService.Instance.CacheDirectory;
            try
            {
                Directory.CreateDirectory(dir);
            }
            catch (Exception ex)
            {
                // Directory creation failure means subsequent cache writes will fail; surface as warning.
                Logging.Log.Warn(LogArea, "CreateDirectory failed", ex);
            }
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
    }
}
