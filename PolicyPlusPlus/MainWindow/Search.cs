using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using PolicyPlusCore.Core;
using PolicyPlusCore.Utilities;
using PolicyPlusPlus.Logging;
using PolicyPlusPlus.Services;
using PolicyPlusPlus.ViewModels;
using Windows.System;

namespace PolicyPlusPlus
{
    public sealed partial class MainWindow
    {
        // When true, user is navigating the suggestion list with arrow keys.
        private bool _navigatingSuggestions;
        private List<(
            PolicyPlusPolicy Policy,
            string NameLower,
            string SecondLower,
            string IdLower,
            string DescLower
        )> _searchIndex = new();
        private Dictionary<
            string,
            (
                PolicyPlusPolicy Policy,
                string NameLower,
                string SecondLower,
                string IdLower,
                string DescLower
            )
        > _searchIndexById = new(StringComparer.OrdinalIgnoreCase);
        private CancellationTokenSource? _searchDebounceCts;
        private bool _searchInName = true;
        private bool _searchInId = true;
        private bool _searchInRegistryKey = true;
        private bool _searchInRegistryValue = true;
        private bool _searchInDescription = false;
        private bool _searchInComments = false;
        private readonly Dictionary<string, string> _compComments = new(
            StringComparer.OrdinalIgnoreCase
        );
        private readonly Dictionary<string, string> _userComments = new(
            StringComparer.OrdinalIgnoreCase
        );

        private SearchOptionsViewModel? SearchOptionsVM =>
            (ScaleHost?.Resources?["SearchOptionsVM"] as SearchOptionsViewModel)
            ?? (RootGrid?.Resources?["SearchOptionsVM"] as SearchOptionsViewModel); // include ScaleHost like FilterVM

        private void SyncSearchFlagsFromViewModel()
        {
            var vm = SearchOptionsVM;
            if (vm == null)
                return;
            _searchInName = vm.InName;
            _searchInId = vm.InId;
            _searchInRegistryKey = vm.InRegistryKey;
            _searchInRegistryValue = vm.InRegistryValue;
            _searchInDescription = vm.InDescription;
            _searchInComments = vm.InComments;
        }

        private void ObserveSearchOptions()
        {
            var vm = SearchOptionsVM;
            if (vm == null)
                return;
            vm.PropertyChanged += (_, __) =>
            {
                try
                {
                    SyncSearchFlagsFromViewModel();
                    var q = SearchBox?.Text ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(q))
                        RunAsyncFilterAndBind();
                    else
                        RunAsyncSearchAndBind(q);
                }
                catch (Exception ex)
                {
                    Log.Warn("MainSearch", "ObserveSearchOptions update failed", ex);
                }
            };
            SyncSearchFlagsFromViewModel();
        }

        private void ShowBaselineSuggestions()
        {
            try
            {
                // Build baseline (empty query) suggestions limited to currently loaded policies.
                if (SearchBox == null)
                    return;

                var allowed = new HashSet<string>(
                    _allPolicies.Select(p => p.UniqueID),
                    StringComparer.OrdinalIgnoreCase
                );

                var list = BuildSuggestions(string.Empty, allowed);
                SearchBox.ItemsSource = list;
            }
            catch (Exception ex)
            {
                Log.Warn("MainSearch", "ShowBaselineSuggestions failed", ex);
            }
        }

        // Description index prebuild logic remains unchanged below
        private void StartPrebuildDescIndex()
        {
            try
            {
                var path = _currentAdmxPath;
                var lang = _currentLanguage;
                var fp =
                    (!string.IsNullOrEmpty(path) && !string.IsNullOrEmpty(lang))
                        ? CacheService.ComputeAdmxFingerprint(path!, lang!)
                        : string.Empty;
                var policies = _allPolicies.ToList();
                _ = Task.Run(() =>
                {
                    try
                    {
                        var idx = new NGramTextIndex(2);
                        var items = policies.Select(p =>
                            (
                                id: p.UniqueID,
                                normalizedText: SearchText.Normalize(p.DisplayExplanation)
                            )
                        );
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
                    catch (Exception ex)
                    {
                        Log.Warn("MainSearch", "Background desc index build failed", ex);
                    }
                });
            }
            catch (Exception ex)
            {
                Log.Warn("MainSearch", "StartPrebuildDescIndex setup failed", ex);
            }
        }

        private void RebuildSearchIndex()
        {
            try
            {
                var s = _settingsCache ?? SettingsService.Instance.LoadSettings();
                _settingsCache = s;
                bool secondEnabled = s.SecondLanguageEnabled ?? false;
                string secondLang =
                    (secondEnabled ? (s.SecondLanguage ?? "en-US") : string.Empty) ?? string.Empty;
                string currentLang = s.Language ?? _currentLanguage ?? string.Empty;
                bool useSecond =
                    secondEnabled
                    && !string.IsNullOrEmpty(secondLang)
                    && !string.Equals(secondLang, currentLang, StringComparison.OrdinalIgnoreCase);

                _searchIndex = _allPolicies
                    .Select(p =>
                        (
                            Policy: p,
                            NameLower: SearchText.Normalize(p.DisplayName),
                            SecondLower: useSecond
                                ? SearchText.Normalize(
                                    LocalizedTextService.GetPolicyNameIn(p, secondLang)
                                )
                                : string.Empty,
                            IdLower: SearchText.Normalize(p.UniqueID),
                            DescLower: SearchText.Normalize(p.DisplayExplanation)
                        )
                    )
                    .ToList();
                _searchIndexById = new Dictionary<
                    string,
                    (
                        PolicyPlusPolicy Policy,
                        string NameLower,
                        string SecondLower,
                        string IdLower,
                        string DescLower
                    )
                >(StringComparer.OrdinalIgnoreCase);
                foreach (var e in _searchIndex)
                    _searchIndexById[e.Policy.UniqueID] = e;
                // Invalidate second index so it can rebuild with new language if needed
                _secondIndexBuilt = false;
            }
            catch (Exception ex)
            {
                Log.Error("MainSearch", "RebuildSearchIndex failed", ex);
                _searchIndex = new();
                _searchIndexById = new(StringComparer.OrdinalIgnoreCase);
            }
        }

        private static int ScoreMatch(string textLower, string qLower)
        {
            if (string.IsNullOrEmpty(qLower))
                return 0;
            if (string.Equals(textLower, qLower, StringComparison.Ordinal))
                return 100;
            if (textLower.StartsWith(qLower, StringComparison.Ordinal))
                return 60;
            int idx = textLower.IndexOf(qLower, StringComparison.Ordinal);
            if (idx > 0)
            {
                char prev = textLower[idx - 1];
                if (!char.IsLetterOrDigit(prev))
                    return 40;
                return 20;
            }
            return -1000;
        }

        private void SearchClearBtn_Tapped(object sender, TappedRoutedEventArgs e)
        {
            try
            {
                _navTyping = false;
                if (SearchBox != null)
                    SearchBox.Text = string.Empty;
                UpdateSearchClearButtonVisibility();
                RunAsyncFilterAndBind();
                UpdateNavButtons();
                e.Handled = true;
            }
            catch (Exception ex)
            {
                Log.Warn("MainSearch", "SearchClearBtn_Tapped failed", ex);
            }
        }

        private void UpdateSearchClearButtonVisibility()
        {
            try
            {
                var btn = RootGrid?.FindName("SearchClearBtn") as UIElement;
                if (btn != null)
                    btn.Visibility = !string.IsNullOrEmpty(SearchBox?.Text)
                        ? Visibility.Visible
                        : Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                Log.Warn("MainSearch", "UpdateSearchClearButtonVisibility failed", ex);
            }
        }

        private void SearchBox_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                HideBuiltInSearchClearButton();
            }
            catch (Exception ex)
            {
                Log.Warn("MainSearch", "HideBuiltInSearchClearButton failed (Loaded)", ex);
            }
            try
            {
                UpdateSearchClearButtonVisibility();
            }
            catch (Exception ex2)
            {
                Log.Warn("MainSearch", "UpdateSearchClearButtonVisibility failed (Loaded)", ex2);
            }
        }

        private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            try
            {
                // When focused with no input, show baseline suggestions (history/ranking-based)
                var q = (SearchBox?.Text ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(q))
                {
                    ShowBaselineSuggestions();
                    try
                    {
                        if (SearchBox != null)
                        {
                            // Try to open the suggestion list explicitly if supported
                            SearchBox.IsSuggestionListOpen = true;
                        }
                    }
                    catch
                    {
                        // Some WinUI versions expose IsSuggestionListOpen as read-only; ignore failures.
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warn("MainSearch", "SearchBox_GotFocus failed", ex);
            }
        }

        private void HideBuiltInSearchClearButton()
        {
            try
            {
                if (SearchBox == null)
                    return;
                var deleteBtn = FindDescendantByName(SearchBox, "DeleteButton") as UIElement;
                if (deleteBtn != null)
                {
                    deleteBtn.Visibility = Visibility.Collapsed;
                    deleteBtn.IsHitTestVisible = false;
                    if (deleteBtn is Control c)
                    {
                        c.IsEnabled = false;
                        c.Opacity = 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warn("MainSearch", "HideBuiltInSearchClearButton core failed", ex);
            }
        }

        private static DependencyObject? FindDescendantByName(DependencyObject? root, string name)
        {
            if (root == null)
                return null;
            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (
                    child is FrameworkElement fe
                    && string.Equals(fe.Name, name, StringComparison.Ordinal)
                )
                    return child;
                var result = FindDescendantByName(child, name);
                if (result != null)
                    return result;
            }
            return null;
        }

        private void SearchBox_TextChanged(object sender, AutoSuggestBoxTextChangedEventArgs e)
        {
            UpdateSearchClearButtonVisibility();
            try
            {
                var q = (SearchBox?.Text ?? string.Empty).Trim();
                // If arrow-key navigating suggestions, avoid recomputing suggestions or committing
                if (SearchBox != null && SearchBox.IsSuggestionListOpen && _navigatingSuggestions)
                {
                    return;
                }
                if (string.IsNullOrEmpty(q))
                {
                    _navTyping = false;
                    try
                    {
                        _searchDebounceCts?.Cancel();
                    }
                    catch { }
                    try
                    {
                        RunImmediateFilterAndBind();
                    }
                    catch (Exception ex)
                    {
                        Log.Warn("MainSearch", "RunImmediateFilterAndBind failed (empty)", ex);
                    }
                    try
                    {
                        ShowBaselineSuggestions();
                    }
                    catch (Exception ex2)
                    {
                        Log.Warn("MainSearch", "ShowBaselineSuggestions failed (empty)", ex2);
                    }
                    try
                    {
                        UpdateNavButtons();
                    }
                    catch { }
                    return;
                }
                // Commit only when: user typed, or when a suggestion has been explicitly chosen
                if (e.Reason is AutoSuggestionBoxTextChangeReason.SuggestionChosen)
                {
                    _navTyping = false;
                    RunAsyncSearchAndBind(q);
                    MaybePushCurrentState();
                }
                else if (e.Reason is AutoSuggestionBoxTextChangeReason.UserInput)
                {
                    _navTyping = true;
                    RunAsyncSearchAndBind(q);
                }
            }
            catch (Exception ex)
            {
                Log.Warn("MainSearch", "SearchBox_TextChanged core logic failed", ex);
            }
        }

        private void SearchBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            try
            {
                if (
                    e.Key
                    is VirtualKey.Down
                        or VirtualKey.Up
                        or VirtualKey.PageDown
                        or VirtualKey.PageUp
                )
                {
                    _navigatingSuggestions = true;
                }
            }
            catch (Exception ex)
            {
                Log.Warn("MainSearch", "SearchBox_KeyDown failed", ex);
            }
        }

        private void SearchBox_KeyUp(object sender, KeyRoutedEventArgs e)
        {
            try
            {
                if (
                    e.Key
                    is VirtualKey.Down
                        or VirtualKey.Up
                        or VirtualKey.PageDown
                        or VirtualKey.PageUp
                )
                {
                    _navigatingSuggestions = false;
                }
            }
            catch (Exception ex)
            {
                Log.Warn("MainSearch", "SearchBox_KeyUp failed", ex);
            }
        }

        // XAML event proxies
        private void SearchBox_QuerySubmitted(
            AutoSuggestBox sender,
            AutoSuggestBoxQuerySubmittedEventArgs args
        )
        {
            try
            {
                // Prefer the chosen suggestion text when available; otherwise use the raw query text
                string commitText = string.Empty;
                if (args.ChosenSuggestion is string chosen && !string.IsNullOrWhiteSpace(chosen))
                    commitText = chosen.Trim();
                else
                    commitText = (args.QueryText ?? string.Empty).Trim();

                if (!string.IsNullOrEmpty(commitText))
                {
                    _navTyping = false;
                    try
                    {
                        // Make sure the box reflects the committed text even when UpdateTextOnSelect is false
                        if (
                            SearchBox != null
                            && !string.Equals(
                                SearchBox.Text?.Trim(),
                                commitText,
                                StringComparison.Ordinal
                            )
                        )
                            SearchBox.Text = commitText;
                    }
                    catch { }
                    RunAsyncSearchAndBind(commitText);
                    MaybePushCurrentState();
                }
                else
                {
                    // If Enter pressed on empty query while suggestions are open, do nothing (prevent accidental commit)
                }
            }
            catch (Exception ex)
            {
                Log.Warn("MainSearch", "QuerySubmitted failed", ex);
            }
        }

        private void SearchBox_SuggestionChosen(
            AutoSuggestBox sender,
            AutoSuggestBoxSuggestionChosenEventArgs args
        )
        {
            try
            {
                // No-op: commit is handled in TextChanged when Reason == SuggestionChosen.
            }
            catch (Exception ex)
            {
                Log.Warn("MainSearch", "SuggestionChosen failed", ex);
            }
        }
    }
}
