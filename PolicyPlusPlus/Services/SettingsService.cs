using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using PolicyPlusPlus.Serialization;
using Windows.ApplicationModel;
using Windows.Storage;

namespace PolicyPlusPlus.Services
{
    public sealed partial class SettingsService
    {
        public static SettingsService Instance { get; } = new SettingsService();
        private readonly SemaphoreSlim _sem = new(1, 1);
        private string _baseDir = string.Empty;
        private string SettingsPath => Path.Combine(_baseDir, "settings.json");
        private string HistoryPath => Path.Combine(_baseDir, "history.json");
        private string SearchStatsPath => Path.Combine(_baseDir, "searchstats.json");
        private string BookmarkPath => Path.Combine(_baseDir, "bookmarks.json");
        public string CacheDirectory => Path.Combine(_baseDir, "Cache");
        private AppSettings? _cachedSettings;

        // Raised when any language preference affecting the ADMX cache may have changed.
        // Subscribers can trigger rescans. Event is best-effort and may coalesce rapid updates.
        public event Action? LanguagesChanged;

        // Raised when the ADMX/ADML source root path changes.
        public event Action? SourcesRootChanged;

        // Raised after the entire cache directory is cleared.
        public event Action? CacheCleared;

        private static readonly JsonSerializerOptions PrettyIgnoreNull = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        private static readonly JsonSerializerOptions HistoryJson = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        private SettingsService() { }

        public void Initialize()
        {
            try
            {
                var testDir = Environment.GetEnvironmentVariable("POLICYPLUS_TEST_DATA_DIR");
                if (!string.IsNullOrWhiteSpace(testDir))
                {
                    _baseDir = Path.GetFullPath(testDir);
                    Directory.CreateDirectory(_baseDir);
                    try
                    {
                        Directory.CreateDirectory(CacheDirectory);
                    }
                    catch { }
                    // Apply forced language overrides for UI tests if provided.
                    try
                    {
                        var forceLang = Environment.GetEnvironmentVariable(
                            "POLICYPLUS_FORCE_LANGUAGE"
                        );
                        var forceSecond = Environment.GetEnvironmentVariable(
                            "POLICYPLUS_FORCE_SECOND_LANGUAGE"
                        );
                        var forceSecondEnabled =
                            Environment.GetEnvironmentVariable("POLICYPLUS_FORCE_SECOND_ENABLED")
                            == "1";
                        if (
                            !string.IsNullOrWhiteSpace(forceLang)
                            || !string.IsNullOrWhiteSpace(forceSecond)
                        )
                        {
                            var s = LoadSettings();
                            if (!string.IsNullOrWhiteSpace(forceLang))
                                s.Language = forceLang;
                            if (!string.IsNullOrWhiteSpace(forceSecond))
                            {
                                s.SecondLanguage = forceSecond;
                                s.SecondLanguageEnabled = forceSecondEnabled;
                            }
                            SaveSettings(s);
                        }
                    }
                    catch { }
                    return;
                }
                bool packaged = true;
                try
                {
                    _ = Package.Current;
                }
                catch
                {
                    packaged = false;
                }
                _baseDir = packaged
                    ? ApplicationData.Current.LocalFolder.Path
                    : Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "PolicyPlusPlus"
                    );
                Directory.CreateDirectory(_baseDir);
                try
                {
                    Directory.CreateDirectory(CacheDirectory);
                }
                catch { }
            }
            catch { }
        }

        internal void InitializeForTests(string baseDirectory)
        {
            _sem.Wait();
            try
            {
                _baseDir = baseDirectory;
                Directory.CreateDirectory(_baseDir);
                _cachedSettings = null;
            }
            finally
            {
                _sem.Release();
            }
        }

        private AppSettings LoadFromDisk_NoLock()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var txt = File.ReadAllText(SettingsPath);
                    return JsonSerializer.Deserialize(txt, AppJsonContext.Default.AppSettings)
                        ?? new AppSettings();
                }
            }
            catch { }
            return new AppSettings();
        }

        private void MigrateIfNeeded_NoLock(AppSettings s)
        {
            int ver = s.SchemaVersion ?? 0;
            if (ver < 1)
                ver = 1;
            if (
                s.CustomPol is null
                && (
                    s.CustomPolEnableComputer.HasValue
                    || s.CustomPolEnableUser.HasValue
                    || s.CustomPolCompPath != null
                    || s.CustomPolUserPath != null
                )
            )
            {
                try
                {
                    bool comp = s.CustomPolEnableComputer ?? false;
                    bool user = s.CustomPolEnableUser ?? false;
                    s.CustomPol = new CustomPolSettings
                    {
                        EnableComputer = comp,
                        EnableUser = user,
                        ComputerPath = comp ? s.CustomPolCompPath : null,
                        UserPath = user ? s.CustomPolUserPath : null,
                        Active = comp || user,
                    };
                }
                catch { }
            }
            if (s.CustomPol != null)
            {
                s.CustomPolEnableComputer = null;
                s.CustomPolEnableUser = null;
                s.CustomPolCompPath = null;
                s.CustomPolUserPath = null;
                if (ver < 2)
                    ver = 2;
            }
            s.SchemaVersion = ver;
        }

        private AppSettings GetSettingsInternal()
        {
            if (_cachedSettings == null)
            {
                _cachedSettings = LoadFromDisk_NoLock();
                MigrateIfNeeded_NoLock(_cachedSettings);
            }
            return _cachedSettings;
        }

        public AppSettings LoadSettings()
        {
            _sem.Wait();
            try
            {
                return GetSettingsInternal();
            }
            finally
            {
                _sem.Release();
            }
        }

        public async Task<AppSettings> LoadSettingsAsync()
        {
            await _sem.WaitAsync();
            try
            {
                return GetSettingsInternal();
            }
            finally
            {
                _sem.Release();
            }
        }

        public void SaveSettings(AppSettings s)
        {
            bool fireLang = false;
            bool fireSrc = false;
            _sem.Wait();
            try
            {
                // Compare language-related tuple before persist to decide whether to raise event.
                var before = GetSettingsInternal();
                var beforeLang = (
                    before.Language ?? string.Empty,
                    before.SecondLanguageEnabled ?? false,
                    before.SecondLanguage ?? string.Empty,
                    before.PrimaryLanguageFallbackEnabled ?? false
                );
                var beforePath = before.AdmxSourcePath ?? string.Empty;

                MigrateIfNeeded_NoLock(s);
                _cachedSettings = s ?? new AppSettings();
                File.WriteAllText(
                    SettingsPath,
                    JsonSerializer.Serialize(_cachedSettings, AppJsonContext.Default.AppSettings)
                );

                var after = _cachedSettings;
                var afterLang = (
                    after.Language ?? string.Empty,
                    after.SecondLanguageEnabled ?? false,
                    after.SecondLanguage ?? string.Empty,
                    after.PrimaryLanguageFallbackEnabled ?? false
                );
                var afterPath = after.AdmxSourcePath ?? string.Empty;
                fireLang = !beforeLang.Equals(afterLang);
                fireSrc = !string.Equals(beforePath, afterPath, StringComparison.OrdinalIgnoreCase);
            }
            catch { }
            finally
            {
                _sem.Release();
            }
            // Fire events outside the lock to avoid deadlocks with handlers calling back into SettingsService.
            try
            {
                if (fireLang)
                    LanguagesChanged?.Invoke();
            }
            catch { }
            try
            {
                if (fireSrc)
                    SourcesRootChanged?.Invoke();
            }
            catch { }
        }

        private void Update(Action<AppSettings> mutator)
        {
            bool fireLang = false;
            bool fireSrc = false;
            _sem.Wait();
            try
            {
                var s = GetSettingsInternal();
                // Snapshot relevant language tuple before mutation.
                var beforeLang = (
                    s.Language ?? string.Empty,
                    s.SecondLanguageEnabled ?? false,
                    s.SecondLanguage ?? string.Empty,
                    s.PrimaryLanguageFallbackEnabled ?? false
                );
                var beforePath = s.AdmxSourcePath ?? string.Empty;
                mutator(s);
                File.WriteAllText(
                    SettingsPath,
                    JsonSerializer.Serialize(s, AppJsonContext.Default.AppSettings)
                );
                // Determine change notifications only; actual invocation happens after releasing the lock.
                var afterLang = (
                    s.Language ?? string.Empty,
                    s.SecondLanguageEnabled ?? false,
                    s.SecondLanguage ?? string.Empty,
                    s.PrimaryLanguageFallbackEnabled ?? false
                );
                var afterPath = s.AdmxSourcePath ?? string.Empty;
                fireLang = !beforeLang.Equals(afterLang);
                fireSrc = !string.Equals(beforePath, afterPath, StringComparison.OrdinalIgnoreCase);
            }
            catch { }
            finally
            {
                _sem.Release();
            }
            // Fire events outside the lock to avoid deadlocks with handlers calling back into SettingsService.
            try
            {
                if (fireLang)
                    LanguagesChanged?.Invoke();
            }
            catch { }
            try
            {
                if (fireSrc)
                    SourcesRootChanged?.Invoke();
            }
            catch { }
        }

        public void ReloadFromDisk()
        {
            _sem.Wait();
            try
            {
                _cachedSettings = LoadFromDisk_NoLock();
                MigrateIfNeeded_NoLock(_cachedSettings);
            }
            finally
            {
                _sem.Release();
            }
        }

        // Best-effort purge for files in the Cache directory older than the provided age.
        // Uses both LastWriteTimeUtc and LastAccessTimeUtc where available to avoid deleting
        // recently touched files. Failures are ignored.
        public void PurgeOldCacheEntries(TimeSpan olderThan)
        {
            try
            {
                void PurgeOne(string dir)
                {
                    if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
                        return;
                    var threshold = DateTime.UtcNow - olderThan;
                    foreach (
                        var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)
                    )
                    {
                        try
                        {
                            var fi = new FileInfo(file);
                            var w = fi.LastWriteTimeUtc;
                            DateTime a;
                            try
                            {
                                a = fi.LastAccessTimeUtc;
                            }
                            catch
                            {
                                a = w;
                            }
                            if (w < threshold && a < threshold)
                            {
                                File.SetAttributes(file, System.IO.FileAttributes.Normal);
                                File.Delete(file);
                            }
                        }
                        catch { }
                    }
                    // Attempt to remove any now-empty directories under Cache
                    try
                    {
                        foreach (
                            var subdir in Directory
                                .EnumerateDirectories(dir, "*", SearchOption.AllDirectories)
                                .OrderByDescending(p => p.Length)
                        )
                        {
                            try
                            {
                                if (
                                    Directory.Exists(subdir)
                                    && !Directory.EnumerateFileSystemEntries(subdir).Any()
                                )
                                    Directory.Delete(subdir);
                            }
                            catch { }
                        }
                    }
                    catch { }
                }

                var uiDir = CacheDirectory;
                PurgeOne(uiDir);
                // Also purge Core's cache location used by AdmxCache (LocalAppData\PolicyPlusPlus\Cache)
                try
                {
                    var coreDir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "PolicyPlusPlus",
                        "Cache"
                    );
                    if (!string.Equals(coreDir, uiDir, StringComparison.OrdinalIgnoreCase))
                        PurgeOne(coreDir);
                }
                catch { }
            }
            catch { }
        }

        // Deletes the entire Cache directory and recreates it, then notifies subscribers.
        public async Task<bool> ClearCacheDirectoryAsync()
        {
            string uiDir = CacheDirectory;
            string? coreDir = null;
            try
            {
                coreDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "PolicyPlusPlus",
                    "Cache"
                );
            }
            catch
            {
                coreDir = null;
            }
            try
            {
                // Proactively clear SQLite connection pools to release file handles.
                try
                {
                    PolicyPlusCore.Core.AdmxCacheRuntime.ReleaseSqliteHandles();
                }
                catch { }
                IDisposable? writerLock = null;
                try
                {
                    writerLock = PolicyPlusCore.Core.AdmxCacheRuntime.TryAcquireWriterLock(
                        TimeSpan.FromSeconds(10)
                    );
                    if (writerLock is null)
                    {
                        return false; // cannot clear while another writer holds the lock
                    }
                    await Task.Run(() =>
                    {
                        try
                        {
                            if (Directory.Exists(uiDir))
                            {
                                foreach (
                                    var f in Directory.EnumerateFiles(
                                        uiDir,
                                        "*",
                                        SearchOption.AllDirectories
                                    )
                                )
                                {
                                    try
                                    {
                                        File.SetAttributes(f, System.IO.FileAttributes.Normal);
                                    }
                                    catch { }
                                }
                                for (int attempt = 0; attempt < 3; attempt++)
                                {
                                    try
                                    {
                                        Directory.Delete(uiDir, recursive: true);
                                        break;
                                    }
                                    catch
                                    {
                                        if (attempt == 2)
                                            throw;
                                        try
                                        {
                                            Thread.Sleep(120);
                                        }
                                        catch { }
                                    }
                                }
                            }
                        }
                        catch { }
                        try
                        {
                            Directory.CreateDirectory(uiDir);
                        }
                        catch { }

                        if (!string.IsNullOrWhiteSpace(coreDir))
                        {
                            try
                            {
                                if (
                                    !string.Equals(
                                        coreDir,
                                        uiDir,
                                        StringComparison.OrdinalIgnoreCase
                                    )
                                )
                                {
                                    try
                                    {
                                        if (Directory.Exists(coreDir))
                                        {
                                            foreach (
                                                var f in Directory.EnumerateFiles(
                                                    coreDir,
                                                    "*",
                                                    SearchOption.AllDirectories
                                                )
                                            )
                                            {
                                                try
                                                {
                                                    File.SetAttributes(
                                                        f,
                                                        System.IO.FileAttributes.Normal
                                                    );
                                                }
                                                catch { }
                                            }
                                            for (int attempt = 0; attempt < 3; attempt++)
                                            {
                                                try
                                                {
                                                    Directory.Delete(coreDir, recursive: true);
                                                    break;
                                                }
                                                catch
                                                {
                                                    if (attempt == 2)
                                                        throw;
                                                    try
                                                    {
                                                        Thread.Sleep(120);
                                                    }
                                                    catch { }
                                                }
                                            }
                                        }
                                    }
                                    catch { }
                                    try
                                    {
                                        Directory.CreateDirectory(coreDir);
                                    }
                                    catch { }
                                }
                            }
                            catch { }
                        }
                    });
                }
                finally
                {
                    try
                    {
                        writerLock?.Dispose();
                    }
                    catch { }
                }
            }
            catch
            {
                return false;
            }
            try
            {
                CacheCleared?.Invoke();
            }
            catch { }
            return true;
        }

        // Bookmark lists
        public (Dictionary<string, List<string>> lists, string active) LoadBookmarkListsWithActive()
        {
            _sem.Wait();
            try
            {
                if (File.Exists(BookmarkPath))
                {
                    var txt = File.ReadAllText(BookmarkPath);
                    var data =
                        JsonSerializer.Deserialize(txt, AppJsonContext.Default.BookmarkStore)
                        ?? new BookmarkStore();
                    var lists =
                        data.Lists
                        ?? new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                    if (lists.Count == 0)
                        lists["default"] = new List<string>();
                    var active =
                        string.IsNullOrEmpty(data.Active) || !lists.ContainsKey(data.Active)
                            ? "default"
                            : data.Active;
                    return (lists, active);
                }
                return (
                    new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["default"] = new List<string>(),
                    },
                    "default"
                );
            }
            catch
            {
                return (
                    new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["default"] = new List<string>(),
                    },
                    "default"
                );
            }
            finally
            {
                _sem.Release();
            }
        }

        public Dictionary<string, List<string>> LoadBookmarkLists()
        {
            var (l, _) = LoadBookmarkListsWithActive();
            return l;
        }

        public void SaveBookmarkLists(Dictionary<string, List<string>> lists, string active)
        {
            _sem.Wait();
            try
            {
                var store = new BookmarkStore { Lists = lists, Active = active };
                File.WriteAllText(
                    BookmarkPath,
                    JsonSerializer.Serialize(store, AppJsonContext.Default.BookmarkStore)
                );
            }
            catch { }
            finally
            {
                _sem.Release();
            }
        }

        // Search stats
        public (
            Dictionary<string, int> counts,
            Dictionary<string, DateTime> lastUsed
        ) LoadSearchStats()
        {
            _sem.Wait();
            try
            {
                if (File.Exists(SearchStatsPath))
                {
                    var txt = File.ReadAllText(SearchStatsPath);
                    var data = JsonSerializer.Deserialize<SearchStats>(txt) ?? new SearchStats();
                    return (
                        data.Counts ?? new(StringComparer.OrdinalIgnoreCase),
                        data.LastUsed ?? new(StringComparer.OrdinalIgnoreCase)
                    );
                }
                return (
                    new(StringComparer.OrdinalIgnoreCase),
                    new(StringComparer.OrdinalIgnoreCase)
                );
            }
            catch
            {
                return (
                    new(StringComparer.OrdinalIgnoreCase),
                    new(StringComparer.OrdinalIgnoreCase)
                );
            }
            finally
            {
                _sem.Release();
            }
        }

        public void SaveSearchStats(
            Dictionary<string, int> counts,
            Dictionary<string, DateTime> lastUsed
        )
        {
            _sem.Wait();
            try
            {
                var data = new SearchStats { Counts = counts, LastUsed = lastUsed };
                File.WriteAllText(SearchStatsPath, JsonSerializer.Serialize(data));
            }
            catch { }
            finally
            {
                _sem.Release();
            }
        }

        // History
        public List<HistoryRecord> LoadHistory()
        {
            _sem.Wait();
            try
            {
                if (File.Exists(HistoryPath))
                {
                    var txt = File.ReadAllText(HistoryPath);
                    return JsonSerializer.Deserialize<List<HistoryRecord>>(txt, HistoryJson)
                        ?? new List<HistoryRecord>();
                }
                return new List<HistoryRecord>();
            }
            catch
            {
                return new List<HistoryRecord>();
            }
            finally
            {
                _sem.Release();
            }
        }

        public void SaveHistory(List<HistoryRecord> records)
        {
            _sem.Wait();
            try
            {
                File.WriteAllText(
                    HistoryPath,
                    JsonSerializer.Serialize(records ?? new List<HistoryRecord>(), HistoryJson)
                );
            }
            catch { }
            finally
            {
                _sem.Release();
            }
        }

        // Column layout helpers
        public List<ColumnState> LoadColumnLayout()
        {
            try
            {
                return LoadSettings().ColumnStates ?? new List<ColumnState>();
            }
            catch
            {
                return new List<ColumnState>();
            }
        }

        // Update wrappers
        public void UpdateTheme(string theme) => Update(s => s.Theme = theme);

        public void UpdateScale(string scale) => Update(s => s.UIScale = scale);

        public void UpdateLanguage(string lang) => Update(s => s.Language = lang);

        public void UpdateSecondLanguageEnabled(bool enabled) =>
            Update(s =>
            {
                s.SecondLanguageEnabled = enabled;
                if (enabled && string.IsNullOrEmpty(s.SecondLanguage))
                    s.SecondLanguage = "en-US";
            });

        public void UpdateSecondLanguage(string lang) => Update(s => s.SecondLanguage = lang);

        public void UpdateAdmxSourcePath(string path) => Update(s => s.AdmxSourcePath = path);

        public void UpdateHideEmptyCategories(bool hide) =>
            Update(s => s.HideEmptyCategories = hide);

        public void UpdateShowDetails(bool show) => Update(s => s.ShowDetails = show);

        public void UpdateColumns(ColumnsOptions cols) => Update(s => s.Columns = cols);

        public void UpdateColumnLayout(List<ColumnState> states) =>
            Update(s => s.ColumnStates = states);

        public void UpdateSearchOptions(SearchOptions opts) => Update(s => s.Search = opts);

        public void UpdatePathJoinSymbol(string symbol) =>
            Update(s =>
                s.PathJoinSymbol = string.IsNullOrEmpty(symbol)
                    ? "+"
                    : symbol.Substring(0, Math.Min(1, symbol.Length))
            );

        public void UpdateCategoryPaneWidth(double width) =>
            Update(s => s.CategoryPaneWidth = Math.Max(0, width));

        public void UpdateDetailPaneHeightStar(double star) =>
            Update(s => s.DetailPaneHeightStar = Math.Max(0, star));

        public void UpdateSort(string? column, string? direction) =>
            Update(s =>
            {
                s.SortColumn = column;
                s.SortDirection = direction;
            });

        public void UpdateLimitUnfilteredTo1000(bool enabled) =>
            Update(s => s.LimitUnfilteredTo1000 = enabled);

        public void UpdateConfiguredOnly(bool configuredOnly) =>
            Update(s => s.ConfiguredOnly = configuredOnly);

        public void UpdateBookmarksOnly(bool bookmarksOnly) =>
            Update(s => s.BookmarksOnly = bookmarksOnly);

        public void UpdateCustomPolSettings(
            bool enableComp,
            bool enableUser,
            string? compPath,
            string? userPath
        )
        {
            Update(s =>
            {
                s.CustomPol = new CustomPolSettings
                {
                    EnableComputer = enableComp,
                    EnableUser = enableUser,
                    ComputerPath = enableComp ? compPath : null,
                    UserPath = enableUser ? userPath : null,
                    Active = (enableComp || enableUser),
                };
                s.CustomPolEnableComputer = null;
                s.CustomPolEnableUser = null;
                s.CustomPolCompPath = null;
                s.CustomPolUserPath = null;
            });
        }

        public void UpdateCustomPolActive(bool active) =>
            Update(s =>
            {
                s.CustomPol ??= new CustomPolSettings();
                s.CustomPol.Active =
                    active && (s.CustomPol.EnableComputer || s.CustomPol.EnableUser);
            });

        public void UpdateCustomPol(CustomPolSettings model) =>
            Update(s =>
            {
                s.CustomPol = model;
                s.CustomPolEnableComputer = null;
                s.CustomPolEnableUser = null;
                s.CustomPolCompPath = null;
                s.CustomPolUserPath = null;
            });

        public void UpdatePrimaryLanguageFallback(bool enabled) =>
            Update(s => s.PrimaryLanguageFallbackEnabled = enabled);

        public void UpdateAdmxCacheEnabled(bool enabled) =>
            Update(s => s.AdmxCacheEnabled = enabled);
    }

    public partial class AppSettings
    {
        public int? SchemaVersion { get; set; }
        public string? Theme { get; set; }
        public string? UIScale { get; set; }
        public string? Language { get; set; }
        public string? AdmxSourcePath { get; set; }

        // When null, treated as enabled by default.
        public bool? AdmxCacheEnabled { get; set; }
        public bool? HideEmptyCategories { get; set; }
        public bool? ShowDetails { get; set; }
        public ColumnsOptions? Columns { get; set; }
        public SearchOptions? Search { get; set; }
        public string? PathJoinSymbol { get; set; }
        public bool? SecondLanguageEnabled { get; set; }
        public string? SecondLanguage { get; set; }
        public double? CategoryPaneWidth { get; set; }
        public double? DetailPaneHeightStar { get; set; }
        public string? SortColumn { get; set; }
        public string? SortDirection { get; set; }
        public List<ColumnState>? ColumnStates { get; set; }
        public bool? LimitUnfilteredTo1000 { get; set; }
        public bool? ConfiguredOnly { get; set; }
        public bool? BookmarksOnly { get; set; }
        public bool? CustomPolEnableComputer { get; set; }
        public bool? CustomPolEnableUser { get; set; }
        public string? CustomPolCompPath { get; set; }
        public string? CustomPolUserPath { get; set; }
        public CustomPolSettings? CustomPol { get; set; }
        public bool? PrimaryLanguageFallbackEnabled { get; set; }
    }

    public class CustomPolSettings
    {
        public bool EnableComputer { get; set; }
        public bool EnableUser { get; set; }
        public string? ComputerPath { get; set; }
        public string? UserPath { get; set; }
        public bool Active { get; set; }
    }

    public class ColumnsOptions
    {
        public bool ShowId { get; set; } = true;
        public bool ShowCategory { get; set; }
        public bool ShowTopCategory { get; set; }
        public bool ShowCategoryPath { get; set; }
        public bool ShowApplies { get; set; }
        public bool ShowSupported { get; set; }
        public bool ShowUserState { get; set; } = true;
        public bool ShowComputerState { get; set; } = true;
        public bool ShowBookmark { get; set; } = true;
        public bool ShowSecondName { get; set; }
    }

    public class ColumnState
    {
        public string Key { get; set; } = string.Empty;
        public int Index { get; set; }
        public double Width { get; set; }
        public bool Visible { get; set; }
    }

    public class SearchOptions
    {
        public bool InName { get; set; } = true;
        public bool InId { get; set; } = true;
        public bool InRegistryKey { get; set; } = true;
        public bool InRegistryValue { get; set; } = true;
        public bool InDescription { get; set; }
        public bool InComments { get; set; }
        public bool UseAndMode { get; set; } // When true, treat spaces as AND between tokens
    }

    public class SearchStats
    {
        public Dictionary<string, int>? Counts { get; set; } =
            new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, DateTime>? LastUsed { get; set; } =
            new(StringComparer.OrdinalIgnoreCase);
    }

    public class BookmarkStore
    {
        public Dictionary<string, List<string>>? Lists { get; set; }
        public string? Active { get; set; }
    }
}
