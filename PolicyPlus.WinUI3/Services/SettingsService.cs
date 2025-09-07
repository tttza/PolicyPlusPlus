using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Windows.Storage;
using Windows.ApplicationModel;
using PolicyPlus.WinUI3.Serialization;

namespace PolicyPlus.WinUI3.Services
{
    public sealed class SettingsService
    {
        public static SettingsService Instance { get; } = new SettingsService();

        private readonly object _gate = new();
        private string _baseDir = string.Empty;
        private string SettingsPath => Path.Combine(_baseDir, "settings.json");
        private string HistoryPath => Path.Combine(_baseDir, "history.json");
        private string SearchStatsPath => Path.Combine(_baseDir, "searchstats.json");
        private string BookmarkPath => Path.Combine(_baseDir, "bookmarks.json");
        public string CacheDirectory => Path.Combine(_baseDir, "Cache");

        private JsonSerializerOptions _json = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private static readonly JsonSerializerOptions _historyJson = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private SettingsService() { }

        public void Initialize()
        {
            try
            {
                bool isPackaged = true;
                try { _ = Package.Current; }
                catch { isPackaged = false; }

                if (isPackaged)
                {
                    _baseDir = ApplicationData.Current.LocalFolder.Path;
                }
                else
                {
                    var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    _baseDir = Path.Combine(root, "PolicyPlusMod");
                }
                Directory.CreateDirectory(_baseDir);
                try { Directory.CreateDirectory(CacheDirectory); } catch { }
            }
            catch { }
        }

        // TEST HOOK: allow tests to isolate settings I/O without touching real user data.
        internal void InitializeForTests(string baseDirectory)
        {
            lock (_gate)
            {
                try
                {
                    _baseDir = baseDirectory;
                    Directory.CreateDirectory(_baseDir);
                    try { Directory.CreateDirectory(CacheDirectory); } catch { }
                }
                catch { }
            }
        }

        public AppSettings LoadSettings()
        {
            lock (_gate)
            {
                try
                {
                    if (File.Exists(SettingsPath))
                    {
                        var txt = File.ReadAllText(SettingsPath);
                        var data = JsonSerializer.Deserialize(txt, AppJsonContext.Default.AppSettings);
                        return data ?? new AppSettings();
                    }
                }
                catch { }
                return new AppSettings();
            }
        }

        public void SaveSettings(AppSettings s)
        {
            lock (_gate)
            {
                try
                {
                    File.WriteAllText(SettingsPath, JsonSerializer.Serialize(s ?? new AppSettings(), AppJsonContext.Default.AppSettings));
                }
                catch { }
            }
        }

        // Bookmarks (separate file, no legacy migration needed)
        public Dictionary<string, List<string>> LoadBookmarkLists()
        {
            var (lists, _) = LoadBookmarkListsWithActive();
            return lists;
        }

        public (Dictionary<string, List<string>> lists, string active) LoadBookmarkListsWithActive()
        {
            lock (_gate)
            {
                try
                {
                    if (File.Exists(BookmarkPath))
                    {
                        var txt = File.ReadAllText(BookmarkPath);
                        var data = JsonSerializer.Deserialize(txt, AppJsonContext.Default.BookmarkStore) ?? new BookmarkStore();
                        var lists = data.Lists ?? new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                        if (lists.Count == 0) lists["default"] = new List<string>();
                        var active = string.IsNullOrEmpty(data.Active) || !lists.ContainsKey(data.Active) ? "default" : data.Active;
                        return (lists, active);
                    }
                }
                catch { }
                return (new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase) { ["default"] = new List<string>() }, "default");
            }
        }

        public void SaveBookmarkLists(Dictionary<string, List<string>> lists, string active)
        {
            lock (_gate)
            {
                try
                {
                    var data = new BookmarkStore { Lists = lists, Active = active };
                    File.WriteAllText(BookmarkPath, JsonSerializer.Serialize(data, AppJsonContext.Default.BookmarkStore));
                }
                catch { }
            }
        }

        public void UpdateTheme(string theme)
        {
            var s = LoadSettings();
            s.Theme = theme;
            SaveSettings(s);
        }

        public void UpdateScale(string scale)
        {
            var s = LoadSettings();
            s.UIScale = scale;
            SaveSettings(s);
        }

        public void UpdateLanguage(string lang)
        {
            var s = LoadSettings();
            s.Language = lang;
            SaveSettings(s);
        }

        public void UpdateSecondLanguageEnabled(bool enabled)
        {
            var s = LoadSettings();
            s.SecondLanguageEnabled = enabled;
            if (enabled && string.IsNullOrEmpty(s.SecondLanguage)) s.SecondLanguage = "en-US";
            SaveSettings(s);
        }

        public void UpdateSecondLanguage(string lang)
        {
            var s = LoadSettings();
            s.SecondLanguage = lang;
            SaveSettings(s);
        }

        public void UpdateAdmxSourcePath(string path)
        {
            var s = LoadSettings();
            s.AdmxSourcePath = path;
            SaveSettings(s);
        }

        public void UpdateHideEmptyCategories(bool hide)
        {
            var s = LoadSettings();
            s.HideEmptyCategories = hide;
            SaveSettings(s);
        }

        public void UpdateShowDetails(bool show)
        {
            var s = LoadSettings();
            s.ShowDetails = show;
            SaveSettings(s);
        }

        public void UpdateColumns(ColumnsOptions cols)
        {
            var s = LoadSettings();
            s.Columns = cols;
            SaveSettings(s);
        }

        public void UpdateColumnLayout(List<ColumnState> states)
        {
            var s = LoadSettings();
            s.ColumnStates = states;
            SaveSettings(s);
        }

        public List<ColumnState> LoadColumnLayout()
        {
            try
            {
                var s = LoadSettings();
                return s.ColumnStates ?? new List<ColumnState>();
            }
            catch { return new List<ColumnState>(); }
        }

        public void UpdateSearchOptions(SearchOptions opts)
        {
            var s = LoadSettings();
            s.Search = opts;
            SaveSettings(s);
        }

        public void UpdatePathJoinSymbol(string symbol)
        {
            var s = LoadSettings();
            s.PathJoinSymbol = string.IsNullOrEmpty(symbol) ? "+" : symbol.Substring(0, Math.Min(1, symbol.Length));
            SaveSettings(s);
        }

        public void UpdateShowEnglishNames(bool show)
        {
            var s = LoadSettings();
            s.ShowEnglishNames = show;
            SaveSettings(s);
        }

        public void UpdateCategoryPaneWidth(double width)
        {
            var s = LoadSettings();
            s.CategoryPaneWidth = Math.Max(0, width);
            SaveSettings(s);
        }

        public void UpdateDetailPaneHeightStar(double star)
        {
            var s = LoadSettings();
            s.DetailPaneHeightStar = Math.Max(0, star);
            SaveSettings(s);
        }

        public void UpdateSort(string? column, string? direction)
        {
            var s = LoadSettings();
            s.SortColumn = column;
            s.SortDirection = direction;
            SaveSettings(s);
        }

        public (Dictionary<string, int> counts, Dictionary<string, DateTime> lastUsed) LoadSearchStats()
        {
            lock (_gate)
            {
                try
                {
                    if (File.Exists(SearchStatsPath))
                    {
                        var txt = File.ReadAllText(SearchStatsPath);
                        var data = JsonSerializer.Deserialize<SearchStats>(txt) ?? new SearchStats();
                        return (data.Counts ?? new(), data.LastUsed ?? new());
                    }
                }
                catch { }
                return (new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase), new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase));
            }
        }

        public void SaveSearchStats(Dictionary<string, int> counts, Dictionary<string, DateTime> lastUsed)
        {
            lock (_gate)
            {
                try
                {
                    var data = new SearchStats { Counts = counts, LastUsed = lastUsed };
                    File.WriteAllText(SearchStatsPath, JsonSerializer.Serialize(data));
                }
                catch { }
            }
        }

        public List<HistoryRecord> LoadHistory()
        {
            lock (_gate)
            {
                try
                {
                    if (File.Exists(HistoryPath))
                    {
                        var txt = File.ReadAllText(HistoryPath);
                        var list = JsonSerializer.Deserialize<List<HistoryRecord>>(txt, _historyJson);
                        return list ?? new List<HistoryRecord>();
                    }
                }
                catch { }
                return new List<HistoryRecord>();
            }
        }

        public void SaveHistory(List<HistoryRecord> records)
        {
            lock (_gate)
            {
                try
                {
                    File.WriteAllText(HistoryPath, JsonSerializer.Serialize(records ?? new List<HistoryRecord>(), _historyJson));
                }
                catch { }
            }
        }

        public void UpdateLimitUnfilteredTo1000(bool enabled)
        {
            var s = LoadSettings();
            s.LimitUnfilteredTo1000 = enabled;
            SaveSettings(s);
        }

        public void UpdateConfiguredOnly(bool configuredOnly)
        {
            var s = LoadSettings();
            s.ConfiguredOnly = configuredOnly;
            SaveSettings(s);
        }

        public void UpdateBookmarksOnly(bool bookmarksOnly)
        {
            var s = LoadSettings();
            s.BookmarksOnly = bookmarksOnly;
            SaveSettings(s);
        }
    }

    public class AppSettings
    {
        public string? Theme { get; set; }
        public string? UIScale { get; set; }
        public string? Language { get; set; }
        public string? AdmxSourcePath { get; set; }
        public bool? HideEmptyCategories { get; set; }
        public bool? ShowDetails { get; set; }
        public ColumnsOptions? Columns { get; set; }
        public SearchOptions? Search { get; set; }
        public string? PathJoinSymbol { get; set; }
        public bool? ShowEnglishNames { get; set; }
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
    }

    public class ColumnsOptions
    {
        public bool ShowId { get; set; } = true;
        public bool ShowCategory { get; set; } = false;
        public bool ShowTopCategory { get; set; } = false;
        public bool ShowCategoryPath { get; set; } = false;
        public bool ShowApplies { get; set; } = false;
        public bool ShowSupported { get; set; } = false;
        public bool ShowUserState { get; set; } = true;
        public bool ShowComputerState { get; set; } = true;
        public bool ShowBookmark { get; set; } = true;
        public bool ShowSecondName { get; set; } = false;
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
        public bool InDescription { get; set; } = false;
        public bool InComments { get; set; } = false;
    }

    public class SearchStats
    {
        public Dictionary<string, int>? Counts { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, DateTime>? LastUsed { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    public class BookmarkStore
    {
        public Dictionary<string, List<string>>? Lists { get; set; }
        public string? Active { get; set; }
    }
}
