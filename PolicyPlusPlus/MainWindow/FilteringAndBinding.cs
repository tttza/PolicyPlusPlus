using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Windows.System;
using PolicyPlusPlus.Models;
using PolicyPlusPlus.Services;
using PolicyPlusPlus.Dialogs; // FindByRegistryWinUI
using PolicyPlus.Core.Utilities;
using PolicyPlus.Core.IO;
using PolicyPlus.Core.Core; // SearchText
using System.Threading;
using PolicyPlusPlus.Filtering; // FilterDecisionEngine
using PolicyPlusPlus.Logging; // logging

namespace PolicyPlusPlus
{
    public sealed partial class MainWindow
    {
        private const int LargeResultThreshold = 200;
        private const int SearchInitialDelayMs = 120;
        private const int PartialExpandDelayMs = 260;

        private readonly NGramTextIndex _descIndex = new(2);
        private readonly NGramTextIndex _nameIndex = new(2);
        private readonly NGramTextIndex _secondIndex = new(2); // second language names
        private readonly NGramTextIndex _idIndex = new(2);
        private bool _descIndexBuilt;
        private bool _nameIndexBuilt;
        private bool _secondIndexBuilt;
        private bool _idIndexBuilt;
        private int _searchGeneration;

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
            catch (Exception ex) { Log.Warn("MainFilter", "EnsureDescIndex cache load failed", ex); }
            try
            {
                var items = _allPolicies.Select(p => (id: p.UniqueID, normalizedText: SearchText.Normalize(p.DisplayExplanation)));
                _descIndex.Build(items);
                try
                {
                    if (!string.IsNullOrEmpty(_currentAdmxPath) && !string.IsNullOrEmpty(_currentLanguage))
                    { var fp = CacheService.ComputeAdmxFingerprint(_currentAdmxPath, _currentLanguage); CacheService.SaveNGramSnapshot(_currentAdmxPath, _currentLanguage, fp, "desc", _descIndex.GetSnapshot()); }
                }
                catch (Exception ex) { Log.Warn("MainFilter", "EnsureDescIndex snapshot save failed", ex); }
                _descIndexBuilt = true;
            }
            catch (Exception ex) { Log.Error("MainFilter", "EnsureDescIndex build failed", ex); }
        }

        private void EnsureNameSecondIdIndexes()
        {
            if (!_nameIndexBuilt)
            {
                try
                {
                    if (!string.IsNullOrEmpty(_currentAdmxPath) && !string.IsNullOrEmpty(_currentLanguage))
                    { var fp = CacheService.ComputeAdmxFingerprint(_currentAdmxPath, _currentLanguage); if (CacheService.TryLoadNGramSnapshot(_currentAdmxPath, _currentLanguage, fp, _nameIndex.N, "name", out var snap) && snap != null) { _nameIndex.LoadSnapshot(snap); _nameIndexBuilt = true; } }
                }
                catch (Exception ex) { Log.Warn("MainFilter", "Name index cache load failed", ex); }
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
                    catch (Exception ex) { Log.Error("MainFilter", "Name index build failed", ex); }
                }
            }
            // second language index
            if (!_secondIndexBuilt)
            {
                try
                {
                    var set = SettingsService.Instance.LoadSettings();
                    bool enabled = set.SecondLanguageEnabled ?? false;
                    string? secondLang = enabled ? (set.SecondLanguage ?? "en-US") : null;
                    if (enabled && !string.IsNullOrEmpty(secondLang) && !string.Equals(secondLang, _currentLanguage, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!string.IsNullOrEmpty(_currentAdmxPath))
                        {
                            var fp = CacheService.ComputeAdmxFingerprint(_currentAdmxPath, secondLang);
                            if (CacheService.TryLoadNGramSnapshot(_currentAdmxPath, secondLang, fp, _secondIndex.N, "sec-" + secondLang, out var snap) && snap != null)
                            { _secondIndex.LoadSnapshot(snap); _secondIndexBuilt = true; }
                        }
                        if (!_secondIndexBuilt)
                        {
                            try
                            {
                                var items = _allPolicies.Select(p => (id: p.UniqueID, normalizedText: SearchText.Normalize(LocalizedTextService.GetPolicyNameIn(p, secondLang))));
                                _secondIndex.Build(items);
                                if (!string.IsNullOrEmpty(_currentAdmxPath))
                                { var fp = CacheService.ComputeAdmxFingerprint(_currentAdmxPath, secondLang); CacheService.SaveNGramSnapshot(_currentAdmxPath, secondLang, fp, "sec-" + secondLang, _secondIndex.GetSnapshot()); }
                                _secondIndexBuilt = true;
                            }
                            catch (Exception ex) { Log.Error("MainFilter", "Second language index build failed", ex); }
                        }
                    }
                    else
                    {
                        _secondIndexBuilt = true; // no second language active
                    }
                }
                catch (Exception ex) { Log.Warn("MainFilter", "Second language index setup failed", ex); _secondIndexBuilt = true; }
            }
            if (!_idIndexBuilt)
            {
                try
                {
                    if (!string.IsNullOrEmpty(_currentAdmxPath) && !string.IsNullOrEmpty(_currentLanguage))
                    { var fp = CacheService.ComputeAdmxFingerprint(_currentAdmxPath, _currentLanguage); if (CacheService.TryLoadNGramSnapshot(_currentAdmxPath, _currentLanguage, fp, _idIndex.N, "id", out var snap) && snap != null) { _idIndex.LoadSnapshot(snap); _idIndexBuilt = true; } }
                }
                catch (Exception ex) { Log.Warn("MainFilter", "ID index cache load failed", ex); }
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
                    catch (Exception ex) { Log.Error("MainFilter", "ID index build failed", ex); }
                }
            }
        }

        private HashSet<string>? GetTextCandidates(string qLower)
        {
            HashSet<string>? union = null;
            try
            {
                if (_searchInName || _searchInId) EnsureNameSecondIdIndexes();
                if (_searchInName)
                {
                    var nameSet = _nameIndex.TryQuery(qLower);
                    if (nameSet != null) { union ??= new HashSet<string>(nameSet, StringComparer.OrdinalIgnoreCase); if (!ReferenceEquals(union, nameSet)) union.UnionWith(nameSet); }
                    var secondSet = _secondIndexBuilt ? _secondIndex.TryQuery(qLower) : null;
                    if (secondSet != null) { union ??= new HashSet<string>(secondSet, StringComparer.OrdinalIgnoreCase); if (!ReferenceEquals(union, secondSet)) union.UnionWith(secondSet); }
                }
                if (_searchInId)
                {
                    var idSet = _idIndex.TryQuery(qLower);
                    if (idSet != null) { union ??= new HashSet<string>(idSet, StringComparer.OrdinalIgnoreCase); if (!ReferenceEquals(union, idSet)) union.UnionWith(idSet); }
                }
            }
            catch (Exception ex) { Log.Warn("MainFilter", "GetTextCandidates failed", ex); }
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

        private FilterDecisionResult EvaluateDecision(string? prospectiveSearch = null)
        { bool hasCategory = _selectedCategory != null; bool hasSearch = !string.IsNullOrWhiteSpace(prospectiveSearch ?? SearchBox?.Text); return FilterDecisionEngine.Evaluate(hasCategory, hasSearch, _configuredOnly, _bookmarksOnly, _limitUnfilteredTo1000); }

        private IEnumerable<PolicyPlusPolicy> ApplyBookmarkFilterIfNeeded(IEnumerable<PolicyPlusPolicy> seq)
        { if (!_bookmarksOnly) return seq; try { var ids = BookmarkService.Instance.ActiveIds; if (ids == null || ids.Count == 0) return Array.Empty<PolicyPlusPolicy>(); var set = new HashSet<string>(ids, StringComparer.OrdinalIgnoreCase); return seq.Where(p => set.Contains(p.UniqueID)); } catch (Exception ex) { Log.Warn("MainFilter", "Bookmark filter failed", ex); return seq; } }

        private void ApplyFiltersAndBind(string query = "", PolicyPlusCategory? category = null)
        {
            if (PolicyList == null || PolicyCount == null) return;
            if (category != null) _selectedCategory = category;
            PreserveScrollPosition();
            UpdateSearchPlaceholder();
            var decision = EvaluateDecision(query);
            IEnumerable<PolicyPlusPolicy> seq = BaseSequenceForFilters(includeSubcategories: decision.IncludeSubcategoryPolicies);
            seq = ApplyBookmarkFilterIfNeeded(seq);
            if (!string.IsNullOrWhiteSpace(query)) { seq = MatchPolicies(query, seq, out _); }
            BindSequenceEnhanced(seq, decision);
            RestorePositionOrSelection();
            if (ViewNavigationService.Instance.Current == null) MaybePushCurrentState();
        }

        private bool PolicyMatchesQuery((PolicyPlusPolicy Policy, string NameLower, string SecondLower, string IdLower, string DescLower) e, string query, string qLower, HashSet<string>? descCandidates)
        {
            if (_searchInName && (e.NameLower.Contains(qLower) || (!string.IsNullOrEmpty(e.SecondLower) && e.SecondLower.Contains(qLower)))) return true;
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

        private void BindSequenceEnhanced(IEnumerable<PolicyPlusPolicy> seq, FilterDecisionResult decision)
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
            ordered = primary != null ? (descSort ? seq.OrderByDescending(primary).ThenBy(p => p.DisplayName).ThenBy(p => p.UniqueID).ToList() : seq.OrderBy(primary).ThenBy(p => p.DisplayName).ThenBy(p => p.UniqueID).ToList()) : seq.OrderBy(p => p.DisplayName).ThenBy(p => p.UniqueID).ToList();
            if (decision.Limit.HasValue && ordered.Count > decision.Limit.Value) ordered = ordered.Take(decision.Limit.Value).ToList();
            _visiblePolicies = ordered.ToList();
            bool computeStatesNow = !_navTyping || _visiblePolicies.Count <= LargeResultThreshold;
            if (computeStatesNow) { try { EnsureLocalSources(); } catch { } }
            _rowByPolicyId.Clear();
            var compSrc = computeStatesNow ? _compSource : null; var userSrc = computeStatesNow ? _userSource : null;
            var rows = new List<object>();
            try
            {
                if (decision.ShowSubcategoryHeaders && _selectedCategory != null)
                    foreach (var child in _selectedCategory.Children.OrderBy(c => c.DisplayName)) rows.Add(PolicyListRow.FromCategory(child));
            }
            catch (Exception ex) { Log.Warn("MainFilter", "Subcategory header bind failed", ex); }
            rows.AddRange(ordered.Select(p => (object)PolicyListRow.FromPolicy(p, compSrc, userSrc)));
            foreach (var obj in rows) if (obj is PolicyListRow r && r.Policy != null) _rowByPolicyId[r.Policy.UniqueID] = r;
            PolicyList.ItemsSource = rows; PolicyCount.Text = $"{_visiblePolicies.Count} / {_allPolicies.Count} policies"; TryRestoreSelectionAsync(rows); MaybePushCurrentState();
        }

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
            try { if (SearchSpinner != null) { SearchSpinner.Visibility = Visibility.Visible; SearchSpinner.IsActive = true; } } catch (Exception ex) { Log.Warn("MainFilter", "Spinner show failed (search)", ex); }
            var applies = _appliesFilter; var category = _selectedCategory; var configuredOnly = _configuredOnly; if (configuredOnly) { try { EnsureLocalSources(); } catch (Exception ex) { Log.Warn("MainFilter", "EnsureLocalSources failed (search)", ex); } }
            var comp = _compSource; var user = _userSource; var decision = EvaluateDecision(q);
            _ = System.Threading.Tasks.Task.Run(async () =>
            {
                try { await System.Threading.Tasks.Task.Delay(SearchInitialDelayMs, token); } catch { return; }
                if (token.IsCancellationRequested) { FinishSpinner(); return; }
                var snap = new FilterSnapshot(applies, category, decision.IncludeSubcategoryPolicies, configuredOnly, comp, user);
                List<PolicyPlusPolicy> matches; List<string> suggestions;
                try { var baseSeq = BaseSequenceForFilters(snap); baseSeq = ApplyBookmarkFilterIfNeeded(baseSeq); matches = MatchPolicies(q, baseSeq, out var allowedSet); suggestions = BuildSuggestions(q, allowedSet); }
                catch (Exception ex) { Log.Warn("MainFilter", "Search task match failed", ex); suggestions = new List<string>(); matches = new List<PolicyPlusPolicy>(); }
                if (token.IsCancellationRequested) { FinishSpinner(); return; }
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (token.IsCancellationRequested || gen != _searchGeneration) { FinishSpinner(); return; }
                    try
                    {
                        SearchBox.ItemsSource = suggestions;
                        if (_navTyping && matches.Count > LargeResultThreshold)
                        { var partial = matches.Take(LargeResultThreshold).ToList(); BindSequenceEnhanced(partial, decision); UpdateNavButtons(); ScheduleFullResultBind(gen, q, matches, decision); }
                        else { BindSequenceEnhanced(matches, decision); UpdateNavButtons(); }
                    }
                    catch (Exception ex) { Log.Error("MainFilter", "UI bind failed (search)", ex); }
                    finally { FinishSpinner(); }
                });
            });
            void FinishSpinner() { DispatcherQueue.TryEnqueue(() => { try { if (SearchSpinner != null) { SearchSpinner.IsActive = false; SearchSpinner.Visibility = Visibility.Collapsed; } } catch (Exception ex) { Log.Warn("MainFilter", "Spinner hide failed (search)", ex); } }); }
        }

        private void ScheduleFullResultBind(int gen, string q, List<PolicyPlusPolicy> fullMatches, FilterDecisionResult decision)
        {
            try
            { var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(PartialExpandDelayMs) }; timer.Tick += (s, e) => { timer.Stop(); if (gen != _searchGeneration) return; var current = (SearchBox?.Text ?? string.Empty).Trim(); if (!string.Equals(current, q, StringComparison.Ordinal)) return; try { BindSequenceEnhanced(fullMatches, decision); UpdateNavButtons(); } catch (Exception ex) { Log.Warn("MainFilter", "Full result bind failed", ex); } }; timer.Start(); }
            catch (Exception ex) { Log.Warn("MainFilter", "ScheduleFullResultBind timer failed; falling back", ex); try { BindSequenceEnhanced(fullMatches, decision); UpdateNavButtons(); } catch (Exception ex2) { Log.Error("MainFilter", "Immediate full result bind also failed", ex2); } }
        }

        private void RunAsyncFilterAndBind()
        {
            _searchDebounceCts?.Cancel(); _searchDebounceCts = new System.Threading.CancellationTokenSource(); var token = _searchDebounceCts.Token; try { PreserveScrollPosition(); } catch (Exception ex) { Log.Warn("MainFilter", "PreserveScrollPosition failed", ex); }
            try { if (SearchSpinner != null) { SearchSpinner.Visibility = Visibility.Visible; SearchSpinner.IsActive = true; } } catch (Exception ex) { Log.Warn("MainFilter", "Spinner show failed (filter)", ex); }
            var applies = _appliesFilter; var category = _selectedCategory; var configuredOnly = _configuredOnly; if (configuredOnly) { try { EnsureLocalSources(); } catch (Exception ex) { Log.Warn("MainFilter", "EnsureLocalSources failed (filter)", ex); } }
            var comp = _compSource; var user = _userSource; var decision = EvaluateDecision();
            _ = System.Threading.Tasks.Task.Run(async () =>
            {
                try { await System.Threading.Tasks.Task.Delay(60, token); } catch { return; }
                if (token.IsCancellationRequested) { Finish(); return; }
                List<PolicyPlusPolicy> items; try { var snap = new FilterSnapshot(applies, category, decision.IncludeSubcategoryPolicies, configuredOnly, comp, user); items = ApplyBookmarkFilterIfNeeded(BaseSequenceForFilters(snap)).ToList(); } catch (Exception ex) { Log.Warn("MainFilter", "Filter task sequence build failed", ex); items = new List<PolicyPlusPolicy>(); }
                if (token.IsCancellationRequested) { Finish(); return; }
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (token.IsCancellationRequested) { Finish(); return; }
                    try { BindSequenceEnhanced(items, decision); RestorePositionOrSelection(); UpdateNavButtons(); if (string.IsNullOrWhiteSpace(SearchBox?.Text)) { ShowBaselineSuggestions(); } }
                    catch (Exception ex) { Log.Error("MainFilter", "UI bind failed (filter)", ex); }
                    finally { Finish(); }
                });
            });
            void Finish() { DispatcherQueue.TryEnqueue(() => { try { if (SearchSpinner != null) { SearchSpinner.IsActive = false; SearchSpinner.Visibility = Visibility.Collapsed; } } catch (Exception ex) { Log.Warn("MainFilter", "Spinner hide failed (filter)", ex); } }); }
        }

        private void RunImmediateFilterAndBind()
        { try { var decision = EvaluateDecision(); var seq = BaseSequenceForFilters(includeSubcategories: decision.IncludeSubcategoryPolicies); seq = ApplyBookmarkFilterIfNeeded(seq); BindSequenceEnhanced(seq, decision); UpdateSearchClearButtonVisibility(); ShowBaselineSuggestions(); } catch (Exception ex) { Log.Error("MainFilter", "RunImmediateFilterAndBind failed", ex); } }
    }
}
