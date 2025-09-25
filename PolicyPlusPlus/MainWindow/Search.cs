using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Input;
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
using Windows.UI.Core;

namespace PolicyPlusPlus
{
    public sealed partial class MainWindow
    {
        // When true, user is navigating the suggestion list with arrow keys.
        private bool _navigatingSuggestions;

        // Suppress SearchBox grabbing focus right after startup until explicit user interaction.
        private bool _suppressInitialSearchBoxFocus = true;
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
        private bool _useAndModeFlag = false; // AND mode flag

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
            _useAndModeFlag = vm.UseAndMode;
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
                    // React to search option toggle (e.g., InName/InDescription/AndMode changes)
                    SyncSearchFlagsFromViewModel();
                    var q = SearchBox?.Text ?? string.Empty;
                    Log.Debug(
                        "MainSearch",
                        $"OptionsChanged qLen={q?.Length} inName={_searchInName} inId={_searchInId} inDesc={_searchInDescription} inComments={_searchInComments} andMode={_useAndModeFlag}"
                    );
                    if (string.IsNullOrWhiteSpace(q))
                    {
                        Log.Trace("MainSearch", "OptionsChanged triggers filter (empty query)");
                        RunAsyncFilterAndBind();
                    }
                    else
                    {
                        Log.Trace("MainSearch", "OptionsChanged triggers search (non-empty)");
                        RunAsyncSearchAndBind(q);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warn("MainSearch", "ObserveSearchOptions update failed", ex);
                }
            };
            SyncSearchFlagsFromViewModel();
        }

        private void ShowBaselineSuggestions(bool onlyIfFocused = false)
        {
            try
            {
                // Build baseline (empty query) suggestions limited to currently loaded policies.
                if (SearchBox == null)
                    return;
                if (onlyIfFocused)
                {
                    try
                    {
                        if (SearchBox.FocusState == FocusState.Unfocused)
                            return;
                    }
                    catch { }
                }

                var allowed = new HashSet<string>(
                    _allPolicies.Select(p => p.UniqueID),
                    StringComparer.OrdinalIgnoreCase
                );

                var list = BuildSuggestions(string.Empty, allowed);
                bool show = list != null && list.Count > 1;
                SearchBox.ItemsSource = show ? list : Array.Empty<string>();
                try
                {
                    SearchBox.IsSuggestionListOpen = show;
                }
                catch { }
                Log.Debug(
                    "MainSearch",
                    $"BaselineSuggestions show={show} count={(list?.Count ?? 0)}"
                );
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
                if (_descIndexBuilt)
                    return; // already loaded from cache
                // Skip background prebuild when ADMX cache is disabled (no-cache mode)
                try
                {
                    var s = SettingsService.Instance.LoadSettings();
                    if ((s.AdmxCacheEnabled ?? true) == false)
                        return;
                }
                catch { }
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
                        var swStart = DateTime.UtcNow;
                        idx.Build(items);
                        var snap = idx.GetSnapshot();
                        // Do not persist UI-level N-gram snapshot; ADMX Cache owns search indexing
                        _descIndex.LoadSnapshot(snap);
                        _descIndexBuilt = true;
                        Log.Info(
                            "MainSearch",
                            $"DescIndexPrebuild ok policies={policies.Count} elapsedMs={(int)(DateTime.UtcNow - swStart).TotalMilliseconds}"
                        );
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
            using var scope = LogScope.Debug("MainSearch", "RebuildSearchIndex");
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

                // Build search index: keep primary DisplayName for display; keep secondary language normalized only for matching.
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
                scope.Complete();
                Log.Info(
                    "MainSearch",
                    $"SearchIndex rebuilt count={_searchIndex.Count} secondLang={(useSecond ? secondLang : "(none)")}"
                );
            }
            catch (Exception ex)
            {
                Log.Error("MainSearch", "RebuildSearchIndex failed", ex);
                _searchIndex = new();
                _searchIndexById = new(StringComparer.OrdinalIgnoreCase);
                scope.Capture(ex);
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
                Log.Debug("MainSearch", "ClearButton tapped -> empty query");
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
                Log.Trace(
                    "MainSearch",
                    $"ClearBtn visibility={(btn != null ? btn.Visibility.ToString() : "n/a")}"
                );
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
                // No longer skipping baseline suggestions on first focus.
                if (string.IsNullOrEmpty(q))
                {
                    // On focus, always show baseline suggestions immediately.
                    ShowBaselineSuggestions(onlyIfFocused: false);
                    // ShowBaselineSuggestions already opened/closed based on count
                    Log.Trace("MainSearch", "GotFocus baseline suggestions attempted");
                }
            }
            catch (Exception ex)
            {
                Log.Warn("MainSearch", "SearchBox_GotFocus failed", ex);
            }
        }

        private void SearchBox_LostFocus(object sender, RoutedEventArgs e)
        {
            try
            {
                if (SearchBox != null)
                    SearchBox.IsSuggestionListOpen = false;
                Log.Trace("MainSearch", "LostFocus suggestions closed");
            }
            catch (Exception ex)
            {
                Log.Warn("MainSearch", "SearchBox_LostFocus failed", ex);
            }
        }

        private void SearchBox_GettingFocus(UIElement sender, GettingFocusEventArgs e)
        {
            try
            {
                if (_suppressInitialSearchBoxFocus)
                {
                    // Allow explicit user initiation (mouse/touch/keyboard). Cancel only programmatic.
                    bool userInitiated =
                        e.FocusState == FocusState.Pointer
                        || e.FocusState == FocusState.Keyboard
                        || e.InputDevice
                            is FocusInputDeviceKind.Mouse
                                or FocusInputDeviceKind.Pen
                                or FocusInputDeviceKind.Touch
                                or FocusInputDeviceKind.Keyboard;

                    if (!userInitiated)
                    {
                        e.TryCancel();
                        _suppressInitialSearchBoxFocus = false; // one-shot
                        Log.Debug("MainSearch", "Suppress initial programmatic focus");
                        return;
                    }

                    // User explicitly focusing: allow and turn off suppression.
                    _suppressInitialSearchBoxFocus = false;
                    Log.Trace("MainSearch", "User initiated first focus allowed");
                }
            }
            catch (Exception ex)
            {
                Log.Warn("MainSearch", "SearchBox_GettingFocus failed", ex);
            }
        }

        private void SearchBox_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            try
            {
                // User explicitly clicked the box; allow it to keep focus hereafter.
                _suppressInitialSearchBoxFocus = false;
            }
            catch { }
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
                var reason = e?.Reason ?? AutoSuggestionBoxTextChangeReason.UserInput;
                // If arrow-key navigating suggestions, avoid recomputing suggestions or committing
                if (SearchBox != null && SearchBox.IsSuggestionListOpen && _navigatingSuggestions)
                {
                    Log.Trace("MainSearch", "TextChanged ignored (navigating suggestions)");
                    return;
                }
                // No startup suppression: baseline suggestion behavior follows typing normally.
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
                        ShowBaselineSuggestions(onlyIfFocused: true);
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
                    Log.Debug("MainSearch", "TextChanged empty -> filter only");
                    return;
                }
                // Commit only when: user typed, or when a suggestion has been explicitly chosen
                if (reason is AutoSuggestionBoxTextChangeReason.SuggestionChosen)
                {
                    _navTyping = false;
                    Log.Debug("MainSearch", $"TextChanged commit suggestion q='{q}'");
                    RunAsyncSearchAndBind(q);
                    MaybePushCurrentState();
                    try
                    {
                        if (SearchBox != null)
                            SearchBox.IsSuggestionListOpen = false;
                    }
                    catch { }
                }
                else if (reason is AutoSuggestionBoxTextChangeReason.UserInput)
                {
                    _navTyping = true;
                    Log.Trace("MainSearch", $"TextChanged user input qLen={q.Length}");
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
                    Log.Trace("MainSearch", $"KeyDown nav key={e.Key}");
                }

                // When Tab is pressed in the search box, move focus to the policy list
                // and select its first item so users can immediately navigate with arrow keys.
                if (e.Key is VirtualKey.Tab)
                {
                    // If Shift is held, let default reverse-tab behavior occur.
                    bool shiftDown = false;
                    try
                    {
                        var state = InputKeyboardSource.GetKeyStateForCurrentThread(
                            VirtualKey.Shift
                        );
                        shiftDown =
                            (state & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;
                    }
                    catch
                    {
                        // Best-effort; if API unavailable, treat as no-shift.
                        shiftDown = false;
                    }
                    if (!shiftDown)
                    {
                        try
                        {
                            if (SearchBox != null)
                                SearchBox.IsSuggestionListOpen = false;
                        }
                        catch { }

                        if (PolicyList != null)
                        {
                            object? first = null;
                            if (PolicyList.ItemsSource is System.Collections.IEnumerable seq)
                            {
                                foreach (var it in seq)
                                {
                                    first = it;
                                    break;
                                }
                            }
                            if (first != null)
                            {
                                PolicyList.SelectedItem = first;
                            }
                            // Move focus to the list regardless of whether it's empty.
                            PolicyList.Focus(FocusState.Keyboard);
                        }

                        e.Handled = true;
                        Log.Debug("MainSearch", "Tab moves focus to list");
                        return;
                    }
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
                    Log.Trace("MainSearch", $"KeyUp nav key={e.Key}");
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
                    Log.Info("MainSearch", $"QuerySubmitted commit qLen={commitText.Length}");
                    RunAsyncSearchAndBind(commitText);
                    MaybePushCurrentState();
                    try
                    {
                        if (SearchBox != null)
                            SearchBox.IsSuggestionListOpen = false;
                    }
                    catch { }
                }
                else
                {
                    // If Enter pressed on empty query while suggestions are open, do nothing (prevent accidental commit)
                    Log.Trace("MainSearch", "QuerySubmitted empty ignored");
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
