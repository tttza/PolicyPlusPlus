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
    }
}
