using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

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

        private static string GetBaseName(
            string admxPath,
            string language,
            string fingerprint,
            int n,
            string kind
        )
        {
            var key = $"{admxPath}\n{language}\nN={n}\nFP={fingerprint}\nK={kind}";
            return $"ngram_{Hash(key)}";
        }

        private static string GetBinPath(string baseName) =>
            Path.Combine(EnsureDir(), baseName + ".bin");

        private static string GetBinGzPath(string baseName) =>
            Path.Combine(EnsureDir(), baseName + ".bin.gz");

        private static string GetJsonGzPath(string baseName) =>
            Path.Combine(EnsureDir(), baseName + ".json.gz");

        private static string GetJsonPath(string baseName) =>
            Path.Combine(EnsureDir(), baseName + ".json");

        // Backward path without fingerprint/kind (legacy JSON)
        private static string GetLegacyJsonPath(string admxPath, string language, int n)
        {
            var dir = EnsureDir();
            var key = $"{admxPath}\n{language}\nN={n}";
            var name = $"ngram_{Hash(key)}.json";
            return Path.Combine(dir, name);
        }

        private static bool TryLoadBinary(string path, out NGramTextIndex.NGramSnapshot? snapshot)
        {
            snapshot = null;
            if (!File.Exists(path))
                return false;
            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var br = new BinaryReader(fs, Encoding.UTF8, leaveOpen: false);
                return ReadBinary(br, out snapshot);
            }
            catch
            {
                snapshot = null;
                return false;
            }
        }

        private static bool TryLoadBinaryGz(string path, out NGramTextIndex.NGramSnapshot? snapshot)
        {
            snapshot = null;
            if (!File.Exists(path))
                return false;
            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var gz = new GZipStream(fs, CompressionMode.Decompress, leaveOpen: false);
                using var br = new BinaryReader(gz, Encoding.UTF8, leaveOpen: false);
                return ReadBinary(br, out snapshot);
            }
            catch
            {
                snapshot = null;
                return false;
            }
        }

        private static bool ReadBinary(BinaryReader br, out NGramTextIndex.NGramSnapshot? snapshot)
        {
            snapshot = null;
            // Header
            var magic = br.ReadUInt32(); // 'NGRM' little-endian
            if (magic != 0x4D52474E)
                return false; // NGRM
            byte version = br.ReadByte();
            if (version != 1)
                return false;
            int n = br.ReadInt32();
            int idCount = br.ReadInt32();
            var ids = new string[idCount];
            for (int i = 0; i < idCount; i++)
                ids[i] = br.ReadString();
            int gramCount = br.ReadInt32();
            var postings = new System.Collections.Generic.Dictionary<string, string[]>(
                StringComparer.Ordinal
            );
            for (int g = 0; g < gramCount; g++)
            {
                string gram = br.ReadString();
                int cnt = br.ReadInt32();
                var arr = new string[cnt];
                for (int i = 0; i < cnt; i++)
                {
                    int idx = br.ReadInt32();
                    arr[i] = (idx >= 0 && idx < ids.Length) ? ids[idx] : string.Empty;
                }
                postings[gram] = arr;
            }
            snapshot = new NGramTextIndex.NGramSnapshot { N = n, Postings = postings };
            return true;
        }

        private static void SaveBinary(string path, NGramTextIndex.NGramSnapshot? snapshot)
        {
            try
            {
                if (snapshot == null)
                    return;
                using var fs = new FileStream(
                    path,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.Read
                );
                using var bw = new BinaryWriter(fs, Encoding.UTF8, leaveOpen: false);
                WriteBinary(bw, snapshot);
            }
            catch { }
        }

        private static void SaveBinaryGz(string path, NGramTextIndex.NGramSnapshot? snapshot)
        {
            try
            {
                if (snapshot == null)
                    return;
                using var fs = new FileStream(
                    path,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.Read
                );
                using var gz = new GZipStream(fs, CompressionLevel.SmallestSize, leaveOpen: false);
                using var bw = new BinaryWriter(gz, Encoding.UTF8, leaveOpen: false);
                WriteBinary(bw, snapshot);
            }
            catch { }
        }

        private static void WriteBinary(BinaryWriter bw, NGramTextIndex.NGramSnapshot snapshot)
        {
            // Build id table
            var idSet = new System.Collections.Generic.HashSet<string>(
                StringComparer.OrdinalIgnoreCase
            );
            foreach (var kv in snapshot.Postings)
            {
                foreach (var id in kv.Value)
                    idSet.Add(id ?? string.Empty);
            }
            var ids = idSet.ToArray();
            var indexOf = new System.Collections.Generic.Dictionary<string, int>(
                StringComparer.OrdinalIgnoreCase
            );
            for (int i = 0; i < ids.Length; i++)
                indexOf[ids[i]] = i;

            // Header 'NGRM' ver=1
            bw.Write(0x4D52474E); // NGRM
            bw.Write((byte)1);
            bw.Write(snapshot.N);
            // IDs
            bw.Write(ids.Length);
            foreach (var id in ids)
                bw.Write(id ?? string.Empty);
            // Postings
            bw.Write(snapshot.Postings.Count);
            foreach (var kv in snapshot.Postings)
            {
                bw.Write(kv.Key ?? string.Empty);
                var arr = kv.Value ?? Array.Empty<string>();
                bw.Write(arr.Length);
                foreach (var id in arr)
                {
                    int idx = indexOf.TryGetValue(id ?? string.Empty, out var i) ? i : -1;
                    bw.Write(idx);
                }
            }
        }

        private static bool TryLoadJsonGz(string path, out NGramTextIndex.NGramSnapshot? snapshot)
        {
            snapshot = null;
            if (!File.Exists(path))
                return false;
            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var gz = new GZipStream(fs, CompressionMode.Decompress, leaveOpen: false);
                using var ms = new MemoryStream();
                gz.CopyTo(ms);
                var json = Encoding.UTF8.GetString(ms.ToArray());
                snapshot =
                    System.Text.Json.JsonSerializer.Deserialize<NGramTextIndex.NGramSnapshot>(json);
                return snapshot != null;
            }
            catch
            {
                snapshot = null;
                return false;
            }
        }

        private static void SaveJsonGz(string path, NGramTextIndex.NGramSnapshot snapshot)
        {
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(snapshot);
                using var fs = new FileStream(
                    path,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.Read
                );
                using var gz = new GZipStream(fs, CompressionLevel.SmallestSize, leaveOpen: false);
                var bytes = Encoding.UTF8.GetBytes(json);
                gz.Write(bytes, 0, bytes.Length);
            }
            catch { }
        }

        public static bool TryLoadNGramSnapshot(
            string admxPath,
            string language,
            string fingerprint,
            int n,
            string kind,
            out NGramTextIndex.NGramSnapshot? snapshot
        )
        {
            snapshot = null;
            try
            {
                var baseName = GetBaseName(admxPath, language, fingerprint, n, kind);
                var binGz = GetBinGzPath(baseName);
                if (TryLoadBinaryGz(binGz, out snapshot) && snapshot != null)
                    return true;

                var bin = GetBinPath(baseName);
                if (TryLoadBinary(bin, out snapshot) && snapshot != null)
                    return true;

                var gz = GetJsonGzPath(baseName);
                if (TryLoadJsonGz(gz, out snapshot) && snapshot != null)
                    return true;

                var json = GetJsonPath(baseName);
                if (File.Exists(json))
                {
                    var txt = File.ReadAllText(json);
                    snapshot =
                        System.Text.Json.JsonSerializer.Deserialize<NGramTextIndex.NGramSnapshot>(
                            txt
                        );
                    if (snapshot != null)
                        return true;
                }

                // Legacy JSON
                var legacy = GetLegacyJsonPath(admxPath, language, n);
                if (File.Exists(legacy))
                {
                    var txt = File.ReadAllText(legacy);
                    snapshot =
                        System.Text.Json.JsonSerializer.Deserialize<NGramTextIndex.NGramSnapshot>(
                            txt
                        );
                    if (snapshot != null)
                        return true;
                }
            }
            catch
            {
                snapshot = null;
            }
            return false;
        }

        public static bool TryLoadNGramSnapshot(
            string admxPath,
            string language,
            int n,
            out NGramTextIndex.NGramSnapshot? snapshot
        )
        {
            var fp = ComputeAdmxFingerprint(admxPath, language);
            return TryLoadNGramSnapshot(admxPath, language, fp, n, "desc", out snapshot);
        }

        public static void SaveNGramSnapshot(
            string admxPath,
            string language,
            string fingerprint,
            string kind,
            NGramTextIndex.NGramSnapshot snapshot
        )
        {
            try
            {
                var baseName = GetBaseName(admxPath, language, fingerprint, snapshot?.N ?? 2, kind);
                var binGz = GetBinGzPath(baseName);
                SaveBinaryGz(binGz, snapshot);
                // Optionally also write plain binary or gz JSON for debugging
                // var bin = GetBinPath(baseName); SaveBinary(bin, snapshot);
                // var gz = GetJsonGzPath(baseName); SaveJsonGz(gz, snapshot);
            }
            catch { }
        }

        public static void SaveNGramSnapshot(
            string admxPath,
            string language,
            string fingerprint,
            NGramTextIndex.NGramSnapshot snapshot
        )
        {
            SaveNGramSnapshot(admxPath, language, fingerprint, "desc", snapshot);
        }

        public static void SaveNGramSnapshot(
            string admxPath,
            string language,
            NGramTextIndex.NGramSnapshot snapshot
        )
        {
            var fp = ComputeAdmxFingerprint(admxPath, language);
            SaveNGramSnapshot(admxPath, language, fp, "desc", snapshot);
        }
    }
}
