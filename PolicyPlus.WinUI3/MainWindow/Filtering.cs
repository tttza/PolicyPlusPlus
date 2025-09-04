using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Windows.System;
using PolicyPlus.WinUI3.Models;
using PolicyPlus.WinUI3.Services;
using PolicyPlus.WinUI3.Dialogs; // FindByRegistryWinUI
using PolicyPlus.Core.Utilities;
using PolicyPlus.Core.IO;
using PolicyPlus.Core.Core; // SearchText
using System.Threading;

namespace PolicyPlus.WinUI3
{
    public sealed partial class MainWindow
    {
        private const int LargeResultThreshold = 200;
        private const int SearchInitialDelayMs = 120;
        private const int PartialExpandDelayMs = 260;

        private readonly NGramTextIndex _descIndex = new(2);
        private readonly NGramTextIndex _nameIndex = new(2);
        private readonly NGramTextIndex _enIndex = new(2);
        private readonly NGramTextIndex _idIndex = new(2);
        private bool _descIndexBuilt;
        private bool _nameIndexBuilt;
        private bool _enIndexBuilt;
        private bool _idIndexBuilt;

        private int _searchGeneration; // generation counter to avoid stale updates
        private System.Threading.CancellationTokenSource? _typingRebindCts = null; // explicit init suppress warning

        private void EnsureDescIndex()
        {
            if (_descIndexBuilt) return;
            try
            {
                if (!string.IsNullOrEmpty(_currentAdmxPath) && !string.IsNullOrEmpty(_currentLanguage))
                {
                    var fp = CacheService.ComputeAdmxFingerprint(_currentAdmxPath, _currentLanguage);
                    if (CacheService.TryLoadNGramSnapshot(_currentAdmxPath, _currentLanguage, fp, _descIndex.N, "desc", out var snap) && snap != null)
                    { _descIndex.LoadSnapshot(snap); _descIndexBuilt = true; return; }
                }
            }
            catch { }
            try
            {
                var items = _allPolicies.Select(p => (id: p.UniqueID, normalizedText: SearchText.Normalize(p.DisplayExplanation)));
                _descIndex.Build(items);
                try
                {
                    if (!string.IsNullOrEmpty(_currentAdmxPath) && !string.IsNullOrEmpty(_currentLanguage))
                    { var fp = CacheService.ComputeAdmxFingerprint(_currentAdmxPath, _currentLanguage); CacheService.SaveNGramSnapshot(_currentAdmxPath, _currentLanguage, fp, "desc", _descIndex.GetSnapshot()); }
                }
                catch { }
                _descIndexBuilt = true;
            }
            catch { }
        }

        private void EnsureNameIdEnIndexes()
        {
            if (!_nameIndexBuilt)
            {
                try
                {
                    if (!string.IsNullOrEmpty(_currentAdmxPath) && !string.IsNullOrEmpty(_currentLanguage))
                    { var fp = CacheService.ComputeAdmxFingerprint(_currentAdmxPath, _currentLanguage); if (CacheService.TryLoadNGramSnapshot(_currentAdmxPath, _currentLanguage, fp, _nameIndex.N, "name", out var snap) && snap != null) { _nameIndex.LoadSnapshot(snap); _nameIndexBuilt = true; } }
                }
                catch { }
                if (!_nameIndexBuilt)
                {
                    try
                    {
                        var items = _allPolicies.Select(p => (id: p.UniqueID, normalizedText: SearchText.Normalize(p.DisplayName)));
                        _nameIndex.Build(items);
                        if (!string.IsNullOrEmpty(_currentAdmxPath) && !string.IsNullOrEmpty(_currentLanguage))
                        { var fp = CacheService.ComputeAdmxFingerprint(_currentAdmxPath, _currentLanguage); CacheService.SaveNGramSnapshot(_currentAdmxPath, _currentLanguage, fp, "name", _nameIndex.GetSnapshot()); }
                        _nameIndexBuilt = true;
                    }
                    catch { }
                }
            }
            if (!_enIndexBuilt)
            {
                try
                {
                    if (!string.IsNullOrEmpty(_currentAdmxPath) && !string.IsNullOrEmpty(_currentLanguage))
                    { var fp = CacheService.ComputeAdmxFingerprint(_currentAdmxPath, _currentLanguage); if (CacheService.TryLoadNGramSnapshot(_currentAdmxPath, _currentLanguage, fp, _enIndex.N, "en", out var snap) && snap != null) { _enIndex.LoadSnapshot(snap); _enIndexBuilt = true; } }
                }
                catch { }
                if (!_enIndexBuilt)
                {
                    try
                    {
                        var items = _allPolicies.Select(p => (id: p.UniqueID, normalizedText: SearchText.Normalize(EnglishTextService.GetEnglishPolicyName(p))));
                        _enIndex.Build(items);
                        if (!string.IsNullOrEmpty(_currentAdmxPath) && !string.IsNullOrEmpty(_currentLanguage))
                        { var fp = CacheService.ComputeAdmxFingerprint(_currentAdmxPath, _currentLanguage); CacheService.SaveNGramSnapshot(_currentAdmxPath, _currentLanguage, fp, "en", _enIndex.GetSnapshot()); }
                        _enIndexBuilt = true;
                    }
                    catch { }
                }
            }
            if (!_idIndexBuilt)
            {
                try
                {
                    if (!string.IsNullOrEmpty(_currentAdmxPath) && !string.IsNullOrEmpty(_currentLanguage))
                    { var fp = CacheService.ComputeAdmxFingerprint(_currentAdmxPath, _currentLanguage); if (CacheService.TryLoadNGramSnapshot(_currentAdmxPath, _currentLanguage, fp, _idIndex.N, "id", out var snap) && snap != null) { _idIndex.LoadSnapshot(snap); _idIndexBuilt = true; } }
                }
                catch { }
                if (!_idIndexBuilt)
                {
                    try
                    {
                        var items = _allPolicies.Select(p => (id: p.UniqueID, normalizedText: SearchText.Normalize(p.UniqueID)));
                        _idIndex.Build(items);
                        if (!string.IsNullOrEmpty(_currentAdmxPath) && !string.IsNullOrEmpty(_currentLanguage))
                        { var fp = CacheService.ComputeAdmxFingerprint(_currentAdmxPath, _currentLanguage); CacheService.SaveNGramSnapshot(_currentAdmxPath, _currentLanguage, fp, "id", _idIndex.GetSnapshot()); }
                        _idIndexBuilt = true;
                    }
                    catch { }
                }
            }
        }

        private HashSet<string>? GetTextCandidates(string qLower)
        {
            HashSet<string>? union = null;
            try
            {
                if (_searchInName || _searchInId) EnsureNameIdEnIndexes();
                if (_searchInName)
                {
                    var nameSet = _nameIndex.TryQuery(qLower);
                    if (nameSet != null) { union ??= new HashSet<string>(nameSet, StringComparer.OrdinalIgnoreCase); if (!ReferenceEquals(union, nameSet)) union.UnionWith(nameSet); }
                    var enSet = _enIndex.TryQuery(qLower);
                    if (enSet != null) { union ??= new HashSet<string>(enSet, StringComparer.OrdinalIgnoreCase); if (!ReferenceEquals(union, enSet)) union.UnionWith(enSet); }
                }
                if (_searchInId)
                {
                    var idSet = _idIndex.TryQuery(qLower);
                    if (idSet != null) { union ??= new HashSet<string>(idSet, StringComparer.OrdinalIgnoreCase); if (!ReferenceEquals(union, idSet)) union.UnionWith(idSet); }
                }
            }
            catch { }
            return union;
        }

        private IEnumerable<PolicyPlusPolicy> BaseSequenceForFilters(bool includeSubcategories) => BaseSequenceForFilters(new FilterSnapshot(_appliesFilter, _selectedCategory, includeSubcategories, _configuredOnly, _compSource, _userSource));

        private readonly struct FilterSnapshot
        {
            public FilterSnapshot(AdmxPolicySection applies, PolicyPlusCategory? category, bool includeSubcats, bool configuredOnly, IPolicySource? comp, IPolicySource? user)
            { Applies = applies; Category = category; IncludeSubcategories = includeSubcats; ConfiguredOnly = configuredOnly; CompSource = comp; UserSource = user; }
            public AdmxPolicySection Applies { get; }
            public PolicyPlusCategory? Category { get; }
            public bool IncludeSubcategories { get; }
            public bool ConfiguredOnly { get; }
            public IPolicySource? CompSource { get; }
            public IPolicySource? UserSource { get; }
        }

        private IEnumerable<PolicyPlusPolicy> BaseSequenceForFilters(FilterSnapshot snap)
        {
            IEnumerable<PolicyPlusPolicy> seq = _allPolicies;
            if (snap.Applies == AdmxPolicySection.Machine) seq = seq.Where(p => p.RawPolicy.Section == AdmxPolicySection.Machine || p.RawPolicy.Section == AdmxPolicySection.Both);
            else if (snap.Applies == AdmxPolicySection.User) seq = seq.Where(p => p.RawPolicy.Section == AdmxPolicySection.User || p.RawPolicy.Section == AdmxPolicySection.Both);
            if (snap.Category is not null)
            {
                if (snap.IncludeSubcategories) { var allowed = new HashSet<string>(); CollectPoliciesRecursive(snap.Category, allowed); seq = seq.Where(p => allowed.Contains(p.UniqueID)); }
                else { var direct = new HashSet<string>(snap.Category.Policies.Select(p => p.UniqueID)); seq = seq.Where(p => direct.Contains(p.UniqueID)); }
            }
            if (snap.ConfiguredOnly)
            {
                var pending = PendingChangesService.Instance.Pending?.ToList() ?? new List<PendingChange>();
                if (snap.CompSource != null || snap.UserSource != null || pending.Count > 0)
                {
                    var compLocal = snap.CompSource; var userLocal = snap.UserSource;
                    seq = seq.Where(p =>
                    {
                        try
                        {
                            bool effUser = false, effComp = false;
                            if (p.RawPolicy.Section == AdmxPolicySection.User || p.RawPolicy.Section == AdmxPolicySection.Both)
                            {
                                var pu = pending.FirstOrDefault(pc => pc.PolicyId == p.UniqueID && pc.Scope.Equals("User", StringComparison.OrdinalIgnoreCase));
                                effUser = pu != null ? (pu.DesiredState == PolicyState.Enabled || pu.DesiredState == PolicyState.Disabled) : (userLocal != null && (PolicyProcessing.GetPolicyState(userLocal, p) is PolicyState.Enabled or PolicyState.Disabled));
                            }
                            if (p.RawPolicy.Section == AdmxPolicySection.Machine || p.RawPolicy.Section == AdmxPolicySection.Both)
                            {
                                var pc = pending.FirstOrDefault(pc2 => pc2.PolicyId == p.UniqueID && pc2.Scope.Equals("Computer", StringComparison.OrdinalIgnoreCase));
                                effComp = pc != null ? (pc.DesiredState == PolicyState.Enabled || pc.DesiredState == PolicyState.Disabled) : (compLocal != null && (PolicyProcessing.GetPolicyState(compLocal, p) is PolicyState.Enabled or PolicyState.Disabled));
                            }
                            return effUser || effComp;
                        }
                        catch { return false; }
                    });
                }
            }
            return seq;
        }

        private List<PolicyPlusPolicy> MatchPolicies(string query, IEnumerable<PolicyPlusPolicy> baseSeq, out HashSet<string> allowedSet)
        {
            var qLower = SearchText.Normalize(query);
            allowedSet = new HashSet<string>(baseSeq.Select(p => p.UniqueID), StringComparer.OrdinalIgnoreCase);
            var matched = new List<PolicyPlusPolicy>();
            HashSet<string>? baseCandidates = GetTextCandidates(qLower);
            HashSet<string>? descCandidates = null;
            if (_searchInDescription) { EnsureDescIndex(); descCandidates = _descIndex.TryQuery(qLower); }
            HashSet<string> scanSet = allowedSet;
            if (baseCandidates != null && baseCandidates.Count > 0)
            {
                scanSet = new HashSet<string>(allowedSet, StringComparer.OrdinalIgnoreCase);
                scanSet.IntersectWith(baseCandidates);
                if (descCandidates != null && descCandidates.Count > 0)
                { var tmp = new HashSet<string>(scanSet, StringComparer.OrdinalIgnoreCase); tmp.UnionWith(descCandidates); tmp.IntersectWith(allowedSet); scanSet = tmp; }
            }
            else if (descCandidates != null && descCandidates.Count > 0)
            { scanSet = new HashSet<string>(allowedSet, StringComparer.OrdinalIgnoreCase); scanSet.IntersectWith(descCandidates); }
            bool smallSubset = scanSet.Count > 0 && scanSet.Count < (_allPolicies.Count / 2);
            if (smallSubset)
            {
                foreach (var id in scanSet)
                    if (_searchIndexById.TryGetValue(id, out var e) && PolicyMatchesQuery(e, query, qLower, descCandidates)) matched.Add(e.Policy);
            }
            else
            {
                foreach (var e in _searchIndex)
                    if (scanSet.Contains(e.Policy.UniqueID) && PolicyMatchesQuery(e, query, qLower, descCandidates)) matched.Add(e.Policy);
            }
            return matched;
        }

        private void ApplyFiltersAndBind(string query = "", PolicyPlusCategory? category = null)
        {
            if (PolicyList == null || PolicyCount == null) return;
            if (category != null) _selectedCategory = category;
            PreserveScrollPosition();
            UpdateSearchPlaceholder();
            IEnumerable<PolicyPlusPolicy> seq = BaseSequenceForFilters(includeSubcategories: true);
            if (!string.IsNullOrWhiteSpace(query)) { seq = MatchPolicies(query, seq, out _); }
            BindSequenceEnhanced(seq, flat: true);
            RestorePositionOrSelection();
            if (ViewNavigationService.Instance.Current == null) MaybePushCurrentState();
        }

        private bool PolicyMatchesQuery((PolicyPlusPolicy Policy, string NameLower, string EnglishLower, string IdLower, string DescLower) e, string query, string qLower, HashSet<string>? descCandidates)
        {
            if (_searchInName && (e.NameLower.Contains(qLower) || (!string.IsNullOrEmpty(e.EnglishLower) && e.EnglishLower.Contains(qLower)))) return true;
            if (_searchInId && e.IdLower.Contains(qLower)) return true;
            if (_searchInDescription)
            {
                bool allowDesc = descCandidates == null || descCandidates.Contains(e.Policy.UniqueID);
                if (allowDesc && e.DescLower.Contains(qLower)) return true;
            }
            if (_searchInComments)
            {
                if ((_compComments.TryGetValue(e.Policy.UniqueID, out var c1) && !string.IsNullOrEmpty(c1) && SearchText.Normalize(c1).Contains(qLower)) ||
                    (_userComments.TryGetValue(e.Policy.UniqueID, out var c2) && !string.IsNullOrEmpty(c2) && SearchText.Normalize(c2).Contains(qLower))) return true;
            }
            if (_searchInRegistryKey || _searchInRegistryValue)
            {
                if (_searchInRegistryKey && FindByRegistryWinUI.SearchRegistry(e.Policy, qLower, string.Empty, allowSubstring: true)) return true;
                if (_searchInRegistryValue && FindByRegistryWinUI.SearchRegistryValueNameOnly(e.Policy, qLower, allowSubstring: true)) return true;
            }
            return false;
        }

        private void BindSequenceEnhanced(IEnumerable<PolicyPlusPolicy> seq, bool flat)
        {
            List<PolicyPlusPolicy> ordered;
            Func<PolicyPlusPolicy, object>? primary = null;
            bool descSort = _sortDirection == CommunityToolkit.WinUI.UI.Controls.DataGridSortDirection.Descending;
            if (!string.IsNullOrEmpty(_sortColumn))
            {
                primary = _sortColumn switch
                {
                    nameof(PolicyListRow.DisplayName) => p => p.DisplayName ?? string.Empty,
                    nameof(PolicyListRow.ShortId) => p => { var id = p.UniqueID ?? string.Empty; int i = id.LastIndexOf(':'); return i >= 0 && i + 1 < id.Length ? id[(i + 1)..] : id; },
                    nameof(PolicyListRow.CategoryName) => p => p.Category?.DisplayName ?? string.Empty,
                    nameof(PolicyListRow.TopCategoryName) => p => { var c = p.Category; while (c?.Parent != null) c = c.Parent; return c?.DisplayName ?? string.Empty; },
                    nameof(PolicyListRow.CategoryFullPath) => p => { var parts = new List<string>(); var c = p.Category; while (c != null) { parts.Add(c.DisplayName ?? string.Empty); c = c.Parent; } parts.Reverse(); return string.Join(" / ", parts); },
                    nameof(PolicyListRow.AppliesText) => p => p.RawPolicy.Section switch { AdmxPolicySection.Machine => 1, AdmxPolicySection.User => 2, _ => 0 },
                    nameof(PolicyListRow.SupportedText) => p => p.SupportedOn?.DisplayName ?? string.Empty,
                    _ => null
                };
            }
            ordered = primary != null
                ? (descSort ? seq.OrderByDescending(primary).ThenBy(p => p.DisplayName).ThenBy(p => p.UniqueID).ToList() : seq.OrderBy(primary).ThenBy(p => p.DisplayName).ThenBy(p => p.UniqueID).ToList())
                : seq.OrderBy(p => p.DisplayName).ThenBy(p => p.UniqueID).ToList();
            if (_selectedCategory == null && _limitUnfilteredTo1000 && ordered.Count > 1000) ordered = ordered.Take(1000).ToList();
            _visiblePolicies = ordered.ToList();
            bool computeStatesNow = !_navTyping || _visiblePolicies.Count <= LargeResultThreshold;
            if (computeStatesNow) { try { EnsureLocalSources(); } catch { } }
            _rowByPolicyId.Clear();
            var compSrc = computeStatesNow ? _compSource : null; var userSrc = computeStatesNow ? _userSource : null;

            var rows = new List<object>();
            try
            {
                // Insert subcategories of the selected category (navigation aid) when no active search query
                if (_selectedCategory != null && string.IsNullOrWhiteSpace(SearchBox?.Text))
                {
                    foreach (var child in _selectedCategory.Children.OrderBy(c => c.DisplayName))
                    {
                        rows.Add(PolicyListRow.FromCategory(child));
                    }
                }
            }
            catch { }

            rows.AddRange(ordered.Select(p => (object)PolicyListRow.FromPolicy(p, compSrc, userSrc)));
            foreach (var obj in rows)
                if (obj is PolicyListRow r && r.Policy != null)
                    _rowByPolicyId[r.Policy.UniqueID] = r;
            PolicyList.ItemsSource = rows;
            PolicyCount.Text = $"{_visiblePolicies.Count} / {_allPolicies.Count} policies";
            TryRestoreSelectionAsync(rows);
            MaybePushCurrentState();
        }

        private PolicyPlusPolicy PickRepresentative(IGrouping<string, PolicyPlusPolicy> g)
        { var list = g.ToList(); return list.FirstOrDefault(p => p.RawPolicy.Section == AdmxPolicySection.Both) ?? list.FirstOrDefault(p => p.RawPolicy.Section == AdmxPolicySection.Machine) ?? list[0]; }

        private void CollectPoliciesRecursive(PolicyPlusCategory cat, HashSet<string> sink)
        { foreach (var p in cat.Policies) sink.Add(p.UniqueID); foreach (var child in cat.Children) CollectPoliciesRecursive(child, sink); }

        private void PolicyList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        { var row = PolicyList?.SelectedItem; var p = (row as PolicyListRow)?.Policy ?? row as PolicyPlusPolicy; SetDetails(p); }

        private void SetDetails(PolicyPlusPolicy? p)
        {
            if (DetailTitle == null) return;
            if (p is null)
            { DetailTitle.Blocks.Clear(); DetailId.Blocks.Clear(); DetailCategory.Blocks.Clear(); DetailApplies.Blocks.Clear(); DetailSupported.Blocks.Clear(); if (DetailExplain != null) DetailExplain.Blocks.Clear(); try { if (DetailPlaceholder != null) DetailPlaceholder.Visibility = Visibility.Visible; } catch { } return; }
            try { if (DetailPlaceholder != null) DetailPlaceholder.Visibility = Visibility.Collapsed; } catch { }
            SetPlainText(DetailTitle, p.DisplayName); SetPlainText(DetailId, p.UniqueID);
            SetPlainText(DetailCategory, p.Category is null ? string.Empty : $"Category: {p.Category.DisplayName}");
            var applies = p.RawPolicy.Section switch { AdmxPolicySection.Machine => "Computer", AdmxPolicySection.User => "User", _ => "Both" };
            SetPlainText(DetailApplies, $"Applies to: {applies}");
            SetPlainText(DetailSupported, p.SupportedOn is null ? string.Empty : $"Supported on: {p.SupportedOn.DisplayName}");
            SetExplanationText(p.DisplayExplanation ?? string.Empty);
        }

        private static void SetPlainText(RichTextBlock rtb, string text)
        { rtb.Blocks.Clear(); var p = new Paragraph(); p.Inlines.Add(new Run { Text = text ?? string.Empty }); rtb.Blocks.Add(p); }

        private static bool IsInsideDoubleQuotes(string s, int index)
        { bool inQuote = false; for (int i = 0; i < index; i++) if (s[i] == '"') { int bs = 0; for (int j = i - 1; j >= 0 && s[j] == '\\'; j--) bs++; if ((bs % 2) == 0) inQuote = !inQuote; } return inQuote; }

        private void SetExplanationText(string text)
        {
            if (DetailExplain == null) return; DetailExplain.Blocks.Clear(); var para = new Paragraph(); if (string.IsNullOrEmpty(text)) { DetailExplain.Blocks.Add(para); return; }
            int lastIndex = 0; foreach (Match m in UrlRegex.Matches(text)) { if (IsInsideDoubleQuotes(text, m.Index)) continue; if (m.Index > lastIndex) para.Inlines.Add(new Run { Text = text[lastIndex..m.Index] }); string url = m.Value; var link = new Hyperlink(); link.Inlines.Add(new Run { Text = url }); link.Click += async (s, e) => { if (Uri.TryCreate(url, UriKind.Absolute, out var uri)) { try { await Launcher.LaunchUriAsync(uri); } catch { } } }; para.Inlines.Add(link); lastIndex = m.Index + m.Length; } if (lastIndex < text.Length) para.Inlines.Add(new Run { Text = text[lastIndex..] }); DetailExplain.Blocks.Add(para);
        }

        private void RunAsyncSearchAndBind(string q)
        {
            _searchDebounceCts?.Cancel(); _searchDebounceCts = new System.Threading.CancellationTokenSource(); var token = _searchDebounceCts.Token; int gen = Interlocked.Increment(ref _searchGeneration);
            try { if (SearchSpinner != null) { SearchSpinner.Visibility = Visibility.Visible; SearchSpinner.IsActive = true; } } catch { }
            var applies = _appliesFilter; var category = _selectedCategory; var configuredOnly = _configuredOnly; if (configuredOnly) { try { EnsureLocalSources(); } catch { } }
            var comp = _compSource; var user = _userSource;
            _ = System.Threading.Tasks.Task.Run(async () =>
            {
                try { await System.Threading.Tasks.Task.Delay(SearchInitialDelayMs, token); } catch { return; }
                if (token.IsCancellationRequested) { FinishSpinner(); return; }
                var snap = new FilterSnapshot(applies, category, true, configuredOnly, comp, user);
                List<PolicyPlusPolicy> matches; List<string> suggestions;
                try
                {
                    var baseSeq = BaseSequenceForFilters(snap);
                    matches = MatchPolicies(q, baseSeq, out var allowedSet);
                    suggestions = BuildSuggestions(q, allowedSet);
                }
                catch { suggestions = new List<string>(); matches = new List<PolicyPlusPolicy>(); }
                if (token.IsCancellationRequested) { FinishSpinner(); return; }
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (token.IsCancellationRequested || gen != _searchGeneration) { FinishSpinner(); return; }
                    try
                    {
                        SearchBox.ItemsSource = suggestions; bool flat = true;
                        if (_navTyping && matches.Count > LargeResultThreshold)
                        {
                            var partial = matches.Take(LargeResultThreshold).ToList();
                            BindSequenceEnhanced(partial, flat); UpdateNavButtons();
                            ScheduleFullResultBind(gen, q, matches, flat);
                        }
                        else { BindSequenceEnhanced(matches, flat); UpdateNavButtons(); }
                    }
                    finally { FinishSpinner(); }
                });
            });
            void FinishSpinner() { DispatcherQueue.TryEnqueue(() => { try { if (SearchSpinner != null) { SearchSpinner.IsActive = false; SearchSpinner.Visibility = Visibility.Collapsed; } } catch { } }); }
        }

        private void ScheduleFullResultBind(int gen, string q, List<PolicyPlusPolicy> fullMatches, bool flat)
        {
            try
            {
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(PartialExpandDelayMs) };
                timer.Tick += (s, e) =>
                {
                    timer.Stop();
                    if (gen != _searchGeneration) return;
                    var current = (SearchBox?.Text ?? string.Empty).Trim();
                    if (!string.Equals(current, q, StringComparison.Ordinal)) return;
                    try { BindSequenceEnhanced(fullMatches, flat); UpdateNavButtons(); } catch { }
                };
                timer.Start();
            }
            catch { try { BindSequenceEnhanced(fullMatches, flat); UpdateNavButtons(); } catch { } }
        }

        private void RunAsyncFilterAndBind()
        {
            _searchDebounceCts?.Cancel(); _searchDebounceCts = new System.Threading.CancellationTokenSource(); var token = _searchDebounceCts.Token; try { PreserveScrollPosition(); } catch { }
            try { if (SearchSpinner != null) { SearchSpinner.Visibility = Visibility.Visible; SearchSpinner.IsActive = true; } } catch { }
            var applies = _appliesFilter; var category = _selectedCategory; var configuredOnly = _configuredOnly; if (configuredOnly) { try { EnsureLocalSources(); } catch { } }
            var comp = _compSource; var user = _userSource;
            _ = System.Threading.Tasks.Task.Run(async () =>
            {
                try { await System.Threading.Tasks.Task.Delay(60, token); } catch { return; }
                if (token.IsCancellationRequested) { Finish(); return; }
                List<PolicyPlusPolicy> items; try { var snap = new FilterSnapshot(applies, category, true, configuredOnly, comp, user); items = BaseSequenceForFilters(snap).ToList(); } catch { items = new List<PolicyPlusPolicy>(); }
                if (token.IsCancellationRequested) { Finish(); return; }
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (token.IsCancellationRequested) { Finish(); return; }
                    try { BindSequenceEnhanced(items, flat: true); RestorePositionOrSelection(); UpdateNavButtons(); if (string.IsNullOrWhiteSpace(SearchBox?.Text)) { ShowBaselineSuggestions(); } }
                    finally { Finish(); }
                });
            });
            void Finish() { DispatcherQueue.TryEnqueue(() => { try { if (SearchSpinner != null) { SearchSpinner.IsActive = false; SearchSpinner.Visibility = Visibility.Collapsed; } } catch { } }); }
        }

        private void RunImmediateFilterAndBind()
        { try { var seq = BaseSequenceForFilters(includeSubcategories: true); BindSequenceEnhanced(seq, flat: true); UpdateSearchClearButtonVisibility(); ShowBaselineSuggestions(); } catch { } }
    }
}
