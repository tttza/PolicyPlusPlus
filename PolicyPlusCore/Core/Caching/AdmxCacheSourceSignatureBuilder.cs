using System.Security.Cryptography;
using System.Text;

namespace PolicyPlusCore.Core.Caching;

internal static class AdmxCacheSourceSignatureBuilder
{
    public static Task<string> ComputeSourceSignatureAsync(
        string root,
        string culture,
        CancellationToken ct
    )
    {
        var sb = new StringBuilder(1024);
        HashSet<string>? allowed = null;
        try
        {
            var filter = Environment.GetEnvironmentVariable("POLICYPLUS_CACHE_ONLY_FILES");
            if (!string.IsNullOrWhiteSpace(filter))
                allowed = new HashSet<string>(
                    filter.Split(
                        ';',
                        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
                    ),
                    StringComparer.OrdinalIgnoreCase
                );
        }
        catch { }

        try
        {
            if (allowed != null)
            {
                foreach (var fn in allowed)
                {
                    ct.ThrowIfCancellationRequested();
                    string path;
                    try
                    {
                        path = Path.Combine(root, fn);
                    }
                    catch
                    {
                        continue;
                    }
                    if (!File.Exists(path))
                        continue;
                    try
                    {
                        var fi = new FileInfo(path);
                        sb.Append(fn)
                            .Append('|')
                            .Append(fi.Length)
                            .Append('|')
                            .Append(fi.LastWriteTimeUtc.Ticks)
                            .Append('\n');
                    }
                    catch { }
                }
            }
            else
            {
                foreach (
                    var f in Directory.EnumerateFiles(root, "*.admx", SearchOption.TopDirectoryOnly)
                )
                {
                    ct.ThrowIfCancellationRequested();
                    try
                    {
                        var fi = new FileInfo(f);
                        sb.Append(Path.GetFileName(f))
                            .Append('|')
                            .Append(fi.Length)
                            .Append('|')
                            .Append(fi.LastWriteTimeUtc.Ticks)
                            .Append('\n');
                    }
                    catch { }
                }
            }

            var admlDir = Path.Combine(root, culture);
            if (Directory.Exists(admlDir))
            {
                if (allowed != null)
                {
                    foreach (var fn in allowed)
                    {
                        ct.ThrowIfCancellationRequested();
                        var admlName = Path.GetFileNameWithoutExtension(fn) + ".adml";
                        string path;
                        try
                        {
                            path = Path.Combine(admlDir, admlName);
                        }
                        catch
                        {
                            continue;
                        }
                        if (!File.Exists(path))
                            continue;
                        try
                        {
                            var fi = new FileInfo(path);
                            sb.Append(culture)
                                .Append('/')
                                .Append(admlName)
                                .Append('|')
                                .Append(fi.Length)
                                .Append('|')
                                .Append(fi.LastWriteTimeUtc.Ticks)
                                .Append('\n');
                        }
                        catch { }
                    }
                }
                else
                {
                    foreach (
                        var f in Directory.EnumerateFiles(
                            admlDir,
                            "*.adml",
                            SearchOption.TopDirectoryOnly
                        )
                    )
                    {
                        ct.ThrowIfCancellationRequested();
                        try
                        {
                            var fi = new FileInfo(f);
                            sb.Append(culture)
                                .Append('/')
                                .Append(Path.GetFileName(f))
                                .Append('|')
                                .Append(fi.Length)
                                .Append('|')
                                .Append(fi.LastWriteTimeUtc.Ticks)
                                .Append('\n');
                        }
                        catch { }
                    }
                }
            }
        }
        catch { }

        var data = Encoding.UTF8.GetBytes(sb.ToString());
        string sig;
        try
        {
            using var sha = SHA256.Create();
            sig = Convert.ToHexString(sha.ComputeHash(data));
        }
        catch
        {
            sig = Convert.ToBase64String(data);
        }
        return Task.FromResult(sig);
    }
}
