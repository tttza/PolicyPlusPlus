using System.Globalization;
using Microsoft.Data.Sqlite;
using PolicyPlusCore.Admx;
using PolicyPlusCore.IO;

namespace PolicyPlusCore.Core.Caching;

internal static class AdmxCacheScanOrchestrationService
{
    public static async Task ScanAndUpdateCoreAsync(
        AdmxCacheStore store,
        AdmxCacheFileUsageStore fileUsageStore,
        string? sourceRoot,
        IEnumerable<string> cultures,
        Func<string, IDisposable> scopeFactory,
        Action<string, Exception> traceFileUsageWarning,
        Func<AdmxBundle, string, bool, CancellationToken, Task> diffAndApplyAsync,
        CancellationToken ct
    )
    {
        bool maintenanceDisabled = string.Equals(
            Environment.GetEnvironmentVariable("POLICYPLUS_CACHE_DISABLE_MAINT"),
            "1",
            StringComparison.Ordinal
        );

        string root =
            sourceRoot ?? Environment.ExpandEnvironmentVariables(@"%WINDIR%\PolicyDefinitions");
        if (!Directory.Exists(root))
            return;

        var defaultCulture = CultureInfo.CurrentUICulture.Name;
        bool needGlobalRebuild = await AdmxCacheMetaStore
            .NeedsGlobalRebuildAsync(store, root, ct)
            .ConfigureAwait(false);

        async Task<IReadOnlyList<string>> GetExistingCulturesAsync(CancellationToken token)
        {
            var list = new List<string>(4);
            using var conn = store.OpenConnection();
            await conn.OpenAsync(token).ConfigureAwait(false);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT DISTINCT culture FROM PolicyI18n ORDER BY culture";
            using var rdr = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false);
            while (await rdr.ReadAsync(token).ConfigureAwait(false))
            {
                if (!rdr.IsDBNull(0))
                    list.Add(rdr.GetString(0));
            }
            return list;
        }

        var cultureList = await AdmxCacheScanCulturePlanService
            .BuildCultureListAsync(
                cultures,
                defaultCulture: defaultCulture,
                needGlobalRebuild: needGlobalRebuild,
                getExistingCulturesAsync: GetExistingCulturesAsync,
                ct
            )
            .ConfigureAwait(false);

        bool didGlobalRebuild = false;
        int i = 0;

        List<string> preEnumeratedAdmxFiles = EnumerateAdmxFilesFiltered(root).ToList();

        Task<string?> GetMetaAsync(string key, CancellationToken token) =>
            AdmxCacheMetaStore.GetMetaAsync(store, key, token);
        Task SetMetaAsync(string key, string value, CancellationToken token) =>
            AdmxCacheMetaStore.SetMetaAsync(store, key, value, token);
        Task<string> ComputeSourceSignatureAsync(string r, string c, CancellationToken token) =>
            AdmxCacheSourceSignatureBuilder.ComputeSourceSignatureAsync(r, c, token);
        Task PurgeCultureAsync(string culture, CancellationToken token) =>
            AdmxCacheCulturePurgeService.PurgeCultureAsync(store, culture, token);

        Task<SqliteConnection> OpenFileUsageConnectionAsync(CancellationToken token) =>
            fileUsageStore.OpenConnectionAsync(token);
        Task UpsertFileUsageAsync(
            SqliteConnection conn,
            string path,
            int kind,
            CancellationToken token
        ) => fileUsageStore.UpsertAsync(conn, path, kind, token);

        foreach (var cul in cultureList.Select(AdmxCacheCulture.NormalizeCultureName))
        {
            using var _traceCulture = scopeFactory("ScanAndUpdateAsync.Culture:" + cul);
            bool allowGlobalRebuild = needGlobalRebuild && (i == 0);
            string? currentSig = null;

            if (
                await AdmxCacheScanCultureGateService
                    .TryPurgeAndSkipIfCultureMissingAdmlAsync(root, cul, PurgeCultureAsync, ct)
                    .ConfigureAwait(false)
            )
            {
                i++;
                continue;
            }

            var (skipCulture, computedSig) = await AdmxCacheScanSignatureService
                .ShouldSkipCultureAsync(
                    root,
                    cul,
                    allowGlobalRebuild,
                    GetMetaAsync,
                    ComputeSourceSignatureAsync,
                    ct
                )
                .ConfigureAwait(false);
            currentSig = computedSig;

            AdmxBundle? bundle = null;
            if (!skipCulture)
            {
                bundle = await AdmxCacheScanBundleAndUsageService
                    .LoadBundleAndUpdateUsageAsync(
                        preEnumeratedAdmxFiles,
                        cul,
                        OpenFileUsageConnectionAsync,
                        UpsertFileUsageAsync,
                        DeriveAdmlPath,
                        traceFileUsageWarning,
                        ct
                    )
                    .ConfigureAwait(false);
            }
            else
            {
                await AdmxCacheScanBundleAndUsageService
                    .RefreshUsageOnlyAsync(
                        preEnumeratedAdmxFiles,
                        cul,
                        OpenFileUsageConnectionAsync,
                        UpsertFileUsageAsync,
                        DeriveAdmlPath,
                        ct
                    )
                    .ConfigureAwait(false);
            }

            if (!skipCulture)
            {
                if (allowGlobalRebuild)
                    didGlobalRebuild = true;

                using var _traceDiffApply = scopeFactory(
                    "DiffAndApply:" + cul + (allowGlobalRebuild ? "(global)" : "")
                );
                await diffAndApplyAsync(bundle!, cul, allowGlobalRebuild, ct).ConfigureAwait(false);
                await AdmxCacheScanSignatureService
                    .StoreSignatureAsync(
                        cul,
                        currentSig,
                        root,
                        ComputeSourceSignatureAsync,
                        SetMetaAsync,
                        ct
                    )
                    .ConfigureAwait(false);
            }

            i++;
        }

        await AdmxCacheScanMetaService
            .RecordSourceRootIfGlobalRebuildAsync(didGlobalRebuild, root, SetMetaAsync, ct)
            .ConfigureAwait(false);

        if (!maintenanceDisabled)
        {
            await AdmxCacheMaintenanceService
                .RunOpportunisticMaintenanceAsync(store, ct)
                .ConfigureAwait(false);
        }
    }

    private static IEnumerable<string> EnumerateAdmxFilesFiltered(string root)
    {
        var filterEnv = Environment.GetEnvironmentVariable("POLICYPLUS_CACHE_ONLY_FILES");
        if (!string.IsNullOrWhiteSpace(filterEnv))
        {
            var allowedSet = new HashSet<string>(
                filterEnv.Split(
                    ';',
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
                ),
                StringComparer.OrdinalIgnoreCase
            );
            foreach (var name in allowedSet)
            {
                string p;
                try
                {
                    p = Path.Combine(root, name);
                }
                catch
                {
                    continue;
                }
                if (File.Exists(p))
                    yield return p;
            }
            yield break;
        }

        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(root, "*.admx", SearchOption.TopDirectoryOnly);
        }
        catch
        {
            yield break;
        }

        foreach (var f in files)
            yield return f;
    }

    private static string? DeriveAdmlPath(string admxPath, string culture)
    {
        try
        {
            var dir = Path.GetDirectoryName(admxPath);
            if (string.IsNullOrEmpty(dir))
                return null;
            var cultureDir = Path.Combine(dir, culture);
            var fileName = Path.GetFileNameWithoutExtension(admxPath);
            var candidate = Path.Combine(cultureDir, fileName + ".adml");
            return candidate;
        }
        catch
        {
            return null;
        }
    }
}
