using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Windows.Storage;
using Windows.ApplicationModel;

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

        private JsonSerializerOptions _json = new JsonSerializerOptions
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
            }
            catch { }
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
                        var data = JsonSerializer.Deserialize<AppSettings>(txt, _json);
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
                    File.WriteAllText(SettingsPath, JsonSerializer.Serialize(s ?? new AppSettings(), _json));
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

        public (Dictionary<string, int> counts, Dictionary<string, DateTime> lastUsed) LoadSearchStats()
        {
            lock (_gate)
            {
                try
                {
                    if (File.Exists(SearchStatsPath))
                    {
                        var txt = File.ReadAllText(SearchStatsPath);
                        var data = JsonSerializer.Deserialize<SearchStats>(txt, _json) ?? new SearchStats();
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
                    File.WriteAllText(SearchStatsPath, JsonSerializer.Serialize(data, _json));
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
                        var list = JsonSerializer.Deserialize<List<HistoryRecord>>(txt, _json);
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
                    File.WriteAllText(HistoryPath, JsonSerializer.Serialize(records ?? new List<HistoryRecord>(), _json));
                }
                catch { }
            }
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
    }

    public class ColumnsOptions
    {
        public bool ShowId { get; set; } = true;
        public bool ShowCategory { get; set; } = false;
        public bool ShowApplies { get; set; } = false;
        public bool ShowSupported { get; set; } = false;
        public bool ShowUserState { get; set; } = true;
        public bool ShowComputerState { get; set; } = true;
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
}
