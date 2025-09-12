using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media; // for VisualTreeHelper
using PolicyPlusCore.Core;
using PolicyPlusCore.Utilities;
using PolicyPlusPlus.Logging; // logging
using PolicyPlusPlus.Services;
using PolicyPlusPlus.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PolicyPlusPlus
{
    public sealed partial class MainWindow
    {
        private List<(PolicyPlusPolicy Policy, string NameLower, string SecondLower, string IdLower, string DescLower)> _searchIndex = new();
        private Dictionary<string, (PolicyPlusPolicy Policy, string NameLower, string SecondLower, string IdLower, string DescLower)> _searchIndexById = new(StringComparer.OrdinalIgnoreCase);
        private CancellationTokenSource? _searchDebounceCts;
        private bool _searchInName = true;
        private bool _searchInId = true;
        private bool _searchInRegistryKey = true;
        private bool _searchInRegistryValue = true;
        private bool _searchInDescription = false;
        private bool _searchInComments = false;
        private readonly Dictionary<string, string> _compComments = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _userComments = new(StringComparer.OrdinalIgnoreCase);

        private SearchOptionsViewModel? SearchOptionsVM => (ScaleHost?.Resources?["SearchOptionsVM"] as SearchOptionsViewModel) ?? (RootGrid?.Resources?["SearchOptionsVM"] as SearchOptionsViewModel); // include ScaleHost like FilterVM

        private void SyncSearchFlagsFromViewModel()
        {
            var vm = SearchOptionsVM; if (vm == null) return;
            _searchInName = vm.InName;
            _searchInId = vm.InId;
            _searchInRegistryKey = vm.InRegistryKey;
            _searchInRegistryValue = vm.InRegistryValue;
            _searchInDescription = vm.InDescription;
            _searchInComments = vm.InComments;
        }

        private void ObserveSearchOptions()
        {
            var vm = SearchOptionsVM; if (vm == null) return;
            vm.PropertyChanged += (_, __) =>
            {
                try
                {
                    SyncSearchFlagsFromViewModel();
                    var q = SearchBox?.Text ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(q)) RunAsyncFilterAndBind(); else RunAsyncSearchAndBind(q);
                }
                catch (Exception ex) { Log.Warn("MainSearch", "ObserveSearchOptions update failed", ex); }
            };
            SyncSearchFlagsFromViewModel();
        }

        // Description index prebuild logic remains unchanged below
        private void StartPrebuildDescIndex()
        {
            try
            {
                var path = _currentAdmxPath;
                var lang = _currentLanguage;
                var fp = (!string.IsNullOrEmpty(path) && !string.IsNullOrEmpty(lang)) ? CacheService.ComputeAdmxFingerprint(path!, lang!) : string.Empty;
                var policies = _allPolicies.ToList();
                _ = Task.Run(() =>
                {
                    try
                    {
                        var idx = new NGramTextIndex(2);
                        var items = policies.Select(p => (id: p.UniqueID, normalizedText: SearchText.Normalize(p.DisplayExplanation)));
                        idx.Build(items);
                        var snap = idx.GetSnapshot();
                        if (!string.IsNullOrEmpty(path) && !string.IsNullOrEmpty(lang))
                        {
                            if (!string.IsNullOrEmpty(fp))
                                CacheService.SaveNGramSnapshot(path!, lang!, fp, snap);
                            else
                                CacheService.SaveNGramSnapshot(path!, lang!, snap);
                        }
                        _descIndex.LoadSnapshot(snap);
                        _descIndexBuilt = true;
                    }
                    catch (Exception ex) { Log.Warn("MainSearch", "Background desc index build failed", ex); }
                });
            }
            catch (Exception ex) { Log.Warn("MainSearch", "StartPrebuildDescIndex setup failed", ex); }
        }

        private void RebuildSearchIndex()
        {
            try
            {
                var s = _settingsCache ?? SettingsService.Instance.LoadSettings();
                _settingsCache = s;
                bool secondEnabled = s.SecondLanguageEnabled ?? false;
                string secondLang = (secondEnabled ? (s.SecondLanguage ?? "en-US") : string.Empty) ?? string.Empty;
                string currentLang = s.Language ?? _currentLanguage ?? string.Empty;
                bool useSecond = secondEnabled && !string.IsNullOrEmpty(secondLang) && !string.Equals(secondLang, currentLang, StringComparison.OrdinalIgnoreCase);

                _searchIndex = _allPolicies.Select(p => (
                    Policy: p,
                    NameLower: SearchText.Normalize(p.DisplayName),
                    SecondLower: useSecond ? SearchText.Normalize(LocalizedTextService.GetPolicyNameIn(p, secondLang)) : string.Empty,
                    IdLower: SearchText.Normalize(p.UniqueID),
                    DescLower: SearchText.Normalize(p.DisplayExplanation)
                )).ToList();
                _searchIndexById = new Dictionary<string, (PolicyPlusPolicy Policy, string NameLower, string SecondLower, string IdLower, string DescLower)>(StringComparer.OrdinalIgnoreCase);
                foreach (var e in _searchIndex) _searchIndexById[e.Policy.UniqueID] = e;
                // Invalidate second index so it can rebuild with new language if needed
                _secondIndexBuilt = false;
            }
            catch (Exception ex) { Log.Error("MainSearch", "RebuildSearchIndex failed", ex); _searchIndex = new(); _searchIndexById = new(StringComparer.OrdinalIgnoreCase); }
        }

        private static int ScoreMatch(string textLower, string qLower)
        {
            if (string.IsNullOrEmpty(qLower)) return 0;
            if (string.Equals(textLower, qLower, StringComparison.Ordinal)) return 100;
            if (textLower.StartsWith(qLower, StringComparison.Ordinal)) return 60;
            int idx = textLower.IndexOf(qLower, StringComparison.Ordinal);
            if (idx > 0)
            { char prev = textLower[idx - 1]; if (!char.IsLetterOrDigit(prev)) return 40; return 20; }
            return -1000;
        }

        private List<string> BuildSuggestions(string q, HashSet<string> allowed)
        {
            var qLower = SearchText.Normalize(q);
            var bestByName = new Dictionary<string, (int score, string name)>(StringComparer.OrdinalIgnoreCase);
            bool smallSubset = allowed.Count > 0 && allowed.Count < (_allPolicies.Count / 2);
            void Consider((PolicyPlusPolicy Policy, string NameLower, string SecondLower, string IdLower, string DescLower) e)
            {
                if (!allowed.Contains(e.Policy.UniqueID)) return;
                int score = 0;
                if (!string.IsNullOrEmpty(qLower))
                {
                    int nameScore = ScoreMatch(e.NameLower, qLower);
                    int secondScore = string.IsNullOrEmpty(e.SecondLower) ? -1000 : ScoreMatch(e.SecondLower, qLower);
                    int idScore = ScoreMatch(e.IdLower, qLower);
                    int descScore = _searchInDescription ? ScoreMatch(e.DescLower, qLower) : -1000;
                    if (nameScore <= -1000 && secondScore <= -1000 && idScore <= -1000 && descScore <= -1000) return;
                    score += Math.Max(0, Math.Max(nameScore, secondScore)) * 3;
                    score += Math.Max(0, idScore) * 2;
                    score += Math.Max(0, descScore);
                }
                score += SearchRankingService.GetBoost(e.Policy.UniqueID);
                var name = e.Policy.DisplayName ?? string.Empty; if (string.IsNullOrEmpty(name)) name = e.Policy.UniqueID;
                if (bestByName.TryGetValue(name, out var cur)) { if (score > cur.score) bestByName[name] = (score, name); }
                else bestByName[name] = (score, name);
            }
            if (smallSubset) foreach (var id in allowed) if (_searchIndexById.TryGetValue(id, out var entry)) Consider(entry); else foreach (var e in _searchIndex) Consider(e);
            if (bestByName.Count == 0 && string.IsNullOrEmpty(qLower))
            { foreach (var id in allowed) if (_searchIndexById.TryGetValue(id, out var e)) { int score = SearchRankingService.GetBoost(id); var name = e.Policy.DisplayName ?? string.Empty; if (bestByName.TryGetValue(name, out var cur)) { if (score > cur.score) bestByName[name] = (score, name); } else bestByName[name] = (score, name); } }
            return bestByName.Values.OrderByDescending(v => v.score).ThenBy(v => v.name, StringComparer.InvariantCultureIgnoreCase).Take(10).Select(v => v.name).ToList();
        }

        private void SearchClearBtn_Tapped(object sender, TappedRoutedEventArgs e)
        { try { _navTyping = false; if (SearchBox != null) SearchBox.Text = string.Empty; UpdateSearchClearButtonVisibility(); RunAsyncFilterAndBind(); UpdateNavButtons(); e.Handled = true; } catch (Exception ex) { Log.Warn("MainSearch", "SearchClearBtn_Tapped failed", ex); } }

        private void UpdateSearchClearButtonVisibility()
        { try { var btn = RootGrid?.FindName("SearchClearBtn") as UIElement; if (btn != null) btn.Visibility = !string.IsNullOrEmpty(SearchBox?.Text) ? Visibility.Visible : Visibility.Collapsed; } catch (Exception ex) { Log.Warn("MainSearch", "UpdateSearchClearButtonVisibility failed", ex); } }

        private void SearchBox_Loaded(object sender, RoutedEventArgs e)
        { try { HideBuiltInSearchClearButton(); } catch (Exception ex) { Log.Warn("MainSearch", "HideBuiltInSearchClearButton failed (Loaded)", ex); } try { UpdateSearchClearButtonVisibility(); } catch (Exception ex2) { Log.Warn("MainSearch", "UpdateSearchClearButtonVisibility failed (Loaded)", ex2); } }
        private void HideBuiltInSearchClearButton()
        { try { if (SearchBox == null) return; var deleteBtn = FindDescendantByName(SearchBox, "DeleteButton") as UIElement; if (deleteBtn != null) { deleteBtn.Visibility = Visibility.Collapsed; deleteBtn.IsHitTestVisible = false; if (deleteBtn is Control c) { c.IsEnabled = false; c.Opacity = 0; } } } catch (Exception ex) { Log.Warn("MainSearch", "HideBuiltInSearchClearButton core failed", ex); } }
        private static DependencyObject? FindDescendantByName(DependencyObject? root, string name)
        { if (root == null) return null; int count = VisualTreeHelper.GetChildrenCount(root); for (int i = 0; i < count; i++) { var child = VisualTreeHelper.GetChild(root, i); if (child is FrameworkElement fe && string.Equals(fe.Name, name, StringComparison.Ordinal)) return child; var result = FindDescendantByName(child, name); if (result != null) return result; } return null; }

        private void SearchBox_TextChanged(object sender, AutoSuggestBoxTextChangedEventArgs e)
        {
            UpdateSearchClearButtonVisibility();
            if (e.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                var q = (SearchBox.Text ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(q)) { try { _navTyping = false; } catch (Exception ex) { Log.Warn("MainSearch", "Reset _navTyping failed", ex); } try { _searchDebounceCts?.Cancel(); } catch (Exception ex2) { Log.Warn("MainSearch", "Cancel debounce failed", ex2); } try { RunImmediateFilterAndBind(); } catch (Exception ex3) { Log.Warn("MainSearch", "RunImmediateFilterAndBind failed", ex3); } try { ShowBaselineSuggestions(); } catch (Exception ex4) { Log.Warn("MainSearch", "ShowBaselineSuggestions failed", ex4); } UpdateNavButtons(); return; }
                _navTyping = true; RunAsyncSearchAndBind(q);
            }
        }

        private void ShowBaselineSuggestions()
        { try { if (SearchBox == null) return; var allowed = new HashSet<string>(_allPolicies.Select(p => p.UniqueID), StringComparer.OrdinalIgnoreCase); var list = BuildSuggestions(string.Empty, allowed); SearchBox.ItemsSource = list; } catch (Exception ex) { Log.Warn("MainSearch", "ShowBaselineSuggestions failed", ex); } }

        // XAML event proxies
        private void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            try
            {
                var q = (args.QueryText ?? string.Empty).Trim();
                if (!string.IsNullOrEmpty(q))
                {
                    _navTyping = false;
                    RunAsyncSearchAndBind(q);
                    MaybePushCurrentState();
                }
            }
            catch (Exception ex) { Log.Warn("MainSearch", "QuerySubmitted failed", ex); }
        }

        private void SearchBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            try
            {
                if (args.SelectedItem != null)
                {
                    var chosen = args.SelectedItem.ToString() ?? string.Empty;
                    sender.Text = chosen;
                    _navTyping = false;
                    RunAsyncSearchAndBind(chosen);
                    MaybePushCurrentState();
                }
            }
            catch (Exception ex) { Log.Warn("MainSearch", "SuggestionChosen failed", ex); }
        }

        private void BtnAbout_Click(object sender, RoutedEventArgs e)
        { try { ShowInfo("Policy Plus Mod (WinUI3 preview)"); } catch (Exception ex) { Log.Warn("MainSearch", "BtnAbout_Click failed", ex); } }
    }
}
