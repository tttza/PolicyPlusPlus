// Filtering and binding logic (on-demand sources)
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.UI.Xaml;
using PolicyPlusCore.Core;
using PolicyPlusCore.IO;
using PolicyPlusCore.Utilities;
using PolicyPlusPlus.Dialogs;
using PolicyPlusPlus.Filtering;
using PolicyPlusPlus.Logging;
using PolicyPlusPlus.Models;
using PolicyPlusPlus.Services;
using Windows.System;

namespace PolicyPlusPlus
{
    public sealed partial class MainWindow
    {
        private const int LargeResultThreshold = 200;
        private const int SearchInitialDelayMs = 120;
        private const int PartialExpandDelayMs = 260;
        private readonly NGramTextIndex _descIndex = new(2);
        private readonly NGramTextIndex _nameIndex = new(2);
        private readonly NGramTextIndex _secondIndex = new(2);
        private readonly NGramTextIndex _idIndex = new(2);
        private bool _descIndexBuilt,
            _nameIndexBuilt,
            _secondIndexBuilt,
            _idIndexBuilt;
        private int _searchGeneration;

        private void EnsureDescIndex()
        {
            if (_descIndexBuilt)
                return;
            try
            {
                if (
                    !string.IsNullOrEmpty(_currentAdmxPath)
                    && !string.IsNullOrEmpty(_currentLanguage)
                )
                {
                    var fp = CacheService.ComputeAdmxFingerprint(
                        _currentAdmxPath,
                        _currentLanguage
                    );
                    if (
                        CacheService.TryLoadNGramSnapshot(
                            _currentAdmxPath,
                            _currentLanguage,
                            fp,
                            _descIndex.N,
                            "desc",
                            out var snap
                        )
                        && snap != null
                    )
                    {
                        _descIndex.LoadSnapshot(snap);
                        _descIndexBuilt = true;
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warn("MainFilter", "EnsureDescIndex cache load failed", ex);
            }
            try
            {
                var items = _allPolicies.Select(p =>
                    (id: p.UniqueID, normalizedText: SearchText.Normalize(p.DisplayExplanation))
                );
                _descIndex.Build(items);
                try
                {
                    if (
                        !string.IsNullOrEmpty(_currentAdmxPath)
                        && !string.IsNullOrEmpty(_currentLanguage)
                    )
                    {
                        var fp = CacheService.ComputeAdmxFingerprint(
                            _currentAdmxPath,
                            _currentLanguage
                        );
                        CacheService.SaveNGramSnapshot(
                            _currentAdmxPath,
                            _currentLanguage,
                            fp,
                            "desc",
                            _descIndex.GetSnapshot()
                        );
                    }
                }
                catch (Exception ex)
                {
                    Log.Warn("MainFilter", "EnsureDescIndex snapshot save failed", ex);
                }
                _descIndexBuilt = true;
            }
            catch (Exception ex)
            {
                Log.Error("MainFilter", "EnsureDescIndex build failed", ex);
            }
        }

        private void EnsureNameSecondIdIndexes()
        {
            if (!_nameIndexBuilt)
            {
                try
                {
                    if (
                        !string.IsNullOrEmpty(_currentAdmxPath)
                        && !string.IsNullOrEmpty(_currentLanguage)
                    )
                    {
                        var fp = CacheService.ComputeAdmxFingerprint(
                            _currentAdmxPath,
                            _currentLanguage
                        );
                        if (
                            CacheService.TryLoadNGramSnapshot(
                                _currentAdmxPath,
                                _currentLanguage,
                                fp,
                                _nameIndex.N,
                                "name",
                                out var snap
                            )
                            && snap != null
                        )
                        {
                            _nameIndex.LoadSnapshot(snap);
                            _nameIndexBuilt = true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warn("MainFilter", "Name index cache load failed", ex);
                }
                if (!_nameIndexBuilt)
                {
                    try
                    {
                        var items = _allPolicies.Select(p =>
                            (id: p.UniqueID, normalizedText: SearchText.Normalize(p.DisplayName))
                        );
                        _nameIndex.Build(items);
                        if (
                            !string.IsNullOrEmpty(_currentAdmxPath)
                            && !string.IsNullOrEmpty(_currentLanguage)
                        )
                        {
                            var fp = CacheService.ComputeAdmxFingerprint(
                                _currentAdmxPath,
                                _currentLanguage
                            );
                            CacheService.SaveNGramSnapshot(
                                _currentAdmxPath,
                                _currentLanguage,
                                fp,
                                "name",
                                _nameIndex.GetSnapshot()
                            );
                        }
                        _nameIndexBuilt = true;
                    }
                    catch (Exception ex)
                    {
                        Log.Error("MainFilter", "Name index build failed", ex);
                    }
                }
            }
            if (!_secondIndexBuilt)
            {
                try
                {
                    var set = SettingsService.Instance.LoadSettings();
                    bool enabled = set.SecondLanguageEnabled ?? false;
                    string? secondLang = enabled ? (set.SecondLanguage ?? "en-US") : null;
                    if (
                        enabled
                        && !string.IsNullOrEmpty(secondLang)
                        && !string.Equals(
                            secondLang,
                            _currentLanguage,
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                    {
                        if (!string.IsNullOrEmpty(_currentAdmxPath))
                        {
                            var fp = CacheService.ComputeAdmxFingerprint(
                                _currentAdmxPath,
                                secondLang
                            );
                            if (
                                CacheService.TryLoadNGramSnapshot(
                                    _currentAdmxPath,
                                    secondLang,
                                    fp,
                                    _secondIndex.N,
                                    "sec-" + secondLang,
                                    out var snap
                                )
                                && snap != null
                            )
                            {
                                _secondIndex.LoadSnapshot(snap);
                                _secondIndexBuilt = true;
                            }
                        }
                        if (!_secondIndexBuilt)
                        {
                            try
                            {
                                var items = _allPolicies.Select(p =>
                                    (
                                        id: p.UniqueID,
                                        normalizedText: SearchText.Normalize(
                                            LocalizedTextService.GetPolicyNameIn(p, secondLang)
                                        )
                                    )
                                );
                                _secondIndex.Build(items);
                                if (!string.IsNullOrEmpty(_currentAdmxPath))
                                {
                                    var fp = CacheService.ComputeAdmxFingerprint(
                                        _currentAdmxPath,
                                        secondLang
                                    );
                                    CacheService.SaveNGramSnapshot(
                                        _currentAdmxPath,
                                        secondLang,
                                        fp,
                                        "sec-" + secondLang,
                                        _secondIndex.GetSnapshot()
                                    );
                                }
                                _secondIndexBuilt = true;
                            }
                            catch (Exception ex)
                            {
                                Log.Error("MainFilter", "Second language index build failed", ex);
                            }
                        }
                    }
                    else
                    {
                        _secondIndexBuilt = true; // no second language active
                    }
                }
                catch (Exception ex)
                {
                    Log.Warn("MainFilter", "Second language index setup failed", ex);
                    _secondIndexBuilt = true;
                }
            }
            if (!_idIndexBuilt)
            {
                try
                {
                    if (
                        !string.IsNullOrEmpty(_currentAdmxPath)
                        && !string.IsNullOrEmpty(_currentLanguage)
                    )
                    {
                        var fp = CacheService.ComputeAdmxFingerprint(
                            _currentAdmxPath,
                            _currentLanguage
                        );
                        if (
                            CacheService.TryLoadNGramSnapshot(
                                _currentAdmxPath,
                                _currentLanguage,
                                fp,
                                _idIndex.N,
                                "id",
                                out var snap
                            )
                            && snap != null
                        )
                        {
                            _idIndex.LoadSnapshot(snap);
                            _idIndexBuilt = true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warn("MainFilter", "ID index cache load failed", ex);
                }
                if (!_idIndexBuilt)
                {
                    try
                    {
                        var items = _allPolicies.Select(p =>
                            (id: p.UniqueID, normalizedText: SearchText.Normalize(p.UniqueID))
                        );
                        _idIndex.Build(items);
                        if (
                            !string.IsNullOrEmpty(_currentAdmxPath)
                            && !string.IsNullOrEmpty(_currentLanguage)
                        )
                        {
                            var fp = CacheService.ComputeAdmxFingerprint(
                                _currentAdmxPath,
                                _currentLanguage
                            );
                            CacheService.SaveNGramSnapshot(
                                _currentAdmxPath,
                                _currentLanguage,
                                fp,
                                "id",
                                _idIndex.GetSnapshot()
                            );
                        }
                        _idIndexBuilt = true;
                    }
                    catch (Exception ex)
                    {
                        Log.Error("MainFilter", "ID index build failed", ex);
                    }
                }
            }
        }

        private HashSet<string>? GetTextCandidates(string qLower)
        {
            HashSet<string>? union = null;
            try
            {
                if (_searchInName || _searchInId)
                    EnsureNameSecondIdIndexes();
                if (_searchInName)
                {
                    var nameSet = _nameIndex.TryQuery(qLower);
                    if (nameSet != null)
                    {
                        union ??= new HashSet<string>(nameSet, StringComparer.OrdinalIgnoreCase);
                        if (!ReferenceEquals(union, nameSet))
                            union.UnionWith(nameSet);
                    }
                    var secondSet = _secondIndexBuilt ? _secondIndex.TryQuery(qLower) : null;
                    if (secondSet != null)
                    {
                        union ??= new HashSet<string>(secondSet, StringComparer.OrdinalIgnoreCase);
                        if (!ReferenceEquals(union, secondSet))
                            union.UnionWith(secondSet);
                    }
                }
                if (_searchInId)
                {
                    var idSet = _idIndex.TryQuery(qLower);
                    if (idSet != null)
                    {
                        union ??= new HashSet<string>(idSet, StringComparer.OrdinalIgnoreCase);
                        if (!ReferenceEquals(union, idSet))
                            union.UnionWith(idSet);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warn("MainFilter", "GetTextCandidates failed", ex);
            }
            return union;
        }

        private IEnumerable<PolicyPlusPolicy> BaseSequenceForFilters(bool includeSubcategories)
        {
            var ctx = PolicySourceAccessor.Acquire();
            return BaseSequenceForFilters(
                new FilterSnapshot(
                    _appliesFilter,
                    _selectedCategory,
                    includeSubcategories,
                    _configuredOnly,
                    ctx.Comp,
                    ctx.User
                )
            );
        }

        private readonly struct FilterSnapshot
        {
            public FilterSnapshot(
                AdmxPolicySection applies,
                PolicyPlusCategory? category,
                bool includeSubcats,
                bool configuredOnly,
                IPolicySource? comp,
                IPolicySource? user
            )
            {
                Applies = applies;
                Category = category;
                IncludeSubcategories = includeSubcats;
                ConfiguredOnly = configuredOnly;
                CompSource = comp;
                UserSource = user;
            }

            public AdmxPolicySection Applies { get; }
            public PolicyPlusCategory? Category { get; }
            public bool IncludeSubcategories { get; }
            public bool ConfiguredOnly { get; }
            public IPolicySource? CompSource { get; }
            public IPolicySource? UserSource { get; }
        }

        private void CollectPoliciesRecursive(PolicyPlusCategory cat, HashSet<string> sink)
        {
            foreach (var p in cat.Policies)
                sink.Add(p.UniqueID);
            foreach (var ch in cat.Children)
                CollectPoliciesRecursive(ch, sink);
        }

        private IEnumerable<PolicyPlusPolicy> BaseSequenceForFilters(FilterSnapshot snap)
        {
            IEnumerable<PolicyPlusPolicy> seq = _allPolicies;
            if (snap.Applies == AdmxPolicySection.Machine)
                seq = seq.Where(p =>
                    p.RawPolicy.Section == AdmxPolicySection.Machine
                    || p.RawPolicy.Section == AdmxPolicySection.Both
                );
            else if (snap.Applies == AdmxPolicySection.User)
                seq = seq.Where(p =>
                    p.RawPolicy.Section == AdmxPolicySection.User
                    || p.RawPolicy.Section == AdmxPolicySection.Both
                );
            if (snap.Category != null)
            {
                if (snap.IncludeSubcategories)
                {
                    var allowed = new HashSet<string>();
                    CollectPoliciesRecursive(snap.Category, allowed);
                    seq = seq.Where(p => allowed.Contains(p.UniqueID));
                }
                else
                {
                    var direct = new HashSet<string>(
                        snap.Category.Policies.Select(p => p.UniqueID)
                    );
                    seq = seq.Where(p => direct.Contains(p.UniqueID));
                }
            }
            if (snap.ConfiguredOnly)
            {
                var pending =
                    PendingChangesService.Instance.Pending?.ToList() ?? new List<PendingChange>();
                if (snap.CompSource != null || snap.UserSource != null || pending.Count > 0)
                {
                    var compLocal = snap.CompSource;
                    var userLocal = snap.UserSource;
                    seq = seq.Where(p =>
                    {
                        try
                        {
                            bool effUser = false,
                                effComp = false;
                            if (
                                p.RawPolicy.Section == AdmxPolicySection.User
                                || p.RawPolicy.Section == AdmxPolicySection.Both
                            )
                            {
                                var pu = pending.FirstOrDefault(pc =>
                                    pc.PolicyId == p.UniqueID
                                    && pc.Scope.Equals("User", StringComparison.OrdinalIgnoreCase)
                                );
                                effUser =
                                    pu != null
                                        ? (
                                            pu.DesiredState
                                            is PolicyState.Enabled
                                                or PolicyState.Disabled
                                        )
                                        : (
                                            userLocal != null
                                            && PolicyProcessing.GetPolicyState(userLocal, p)
                                                is PolicyState.Enabled
                                                    or PolicyState.Disabled
                                        );
                            }
                            if (
                                p.RawPolicy.Section == AdmxPolicySection.Machine
                                || p.RawPolicy.Section == AdmxPolicySection.Both
                            )
                            {
                                var pc = pending.FirstOrDefault(pc2 =>
                                    pc2.PolicyId == p.UniqueID
                                    && pc2.Scope.Equals(
                                        "Computer",
                                        StringComparison.OrdinalIgnoreCase
                                    )
                                );
                                effComp =
                                    pc != null
                                        ? (
                                            pc.DesiredState
                                            is PolicyState.Enabled
                                                or PolicyState.Disabled
                                        )
                                        : (
                                            compLocal != null
                                            && PolicyProcessing.GetPolicyState(compLocal, p)
                                                is PolicyState.Enabled
                                                    or PolicyState.Disabled
                                        );
                            }
                            return effUser || effComp;
                        }
                        catch
                        {
                            return false;
                        }
                    });
                }
            }
            return seq;
        }

        private FilterDecisionResult EvaluateDecision(string? prospective = null)
        {
            bool hasCat = _selectedCategory != null;
            bool hasSearch = !string.IsNullOrWhiteSpace(prospective ?? SearchBox?.Text);
            return FilterDecisionEngine.Evaluate(
                hasCat,
                hasSearch,
                _configuredOnly,
                _bookmarksOnly,
                _limitUnfilteredTo1000
            );
        }

        private IEnumerable<PolicyPlusPolicy> ApplyBookmarkFilterIfNeeded(
            IEnumerable<PolicyPlusPolicy> seq
        )
        {
            if (!_bookmarksOnly)
                return seq;
            try
            {
                var ids = BookmarkService.Instance.ActiveIds;
                if (ids == null || ids.Count == 0)
                    return Array.Empty<PolicyPlusPolicy>();
                var set = new HashSet<string>(ids, StringComparer.OrdinalIgnoreCase);
                return seq.Where(p => set.Contains(p.UniqueID));
            }
            catch
            {
                return seq;
            }
        }

        private void ApplyFiltersAndBind(string query = "", PolicyPlusCategory? category = null)
        {
            if (PolicyList == null || PolicyCount == null)
                return;
            if (category != null)
                _selectedCategory = category;
            PreserveScrollPosition();
            UpdateSearchPlaceholder();
            var decision = EvaluateDecision(query);
            IEnumerable<PolicyPlusPolicy> seq = BaseSequenceForFilters(
                decision.IncludeSubcategoryPolicies
            );
            seq = ApplyBookmarkFilterIfNeeded(seq);
            if (!string.IsNullOrWhiteSpace(query))
            {
                seq = MatchPolicies(query, seq, out _);
            }
            BindSequenceEnhanced(seq, decision);
            RestorePositionOrSelection();
            if (ViewNavigationService.Instance.Current == null)
                MaybePushCurrentState();
        }

        private bool PolicyMatchesQuery(
            (
                PolicyPlusPolicy Policy,
                string NameLower,
                string SecondLower,
                string IdLower,
                string DescLower
            ) e,
            string query,
            string qLower,
            HashSet<string>? descCandidates
        )
        {
            if (
                _searchInName
                && (
                    e.NameLower.Contains(qLower)
                    || (!string.IsNullOrEmpty(e.SecondLower) && e.SecondLower.Contains(qLower))
                )
            )
                return true;
            if (_searchInId && e.IdLower.Contains(qLower))
                return true;
            if (_searchInDescription)
            {
                bool allowDesc =
                    descCandidates == null || descCandidates.Contains(e.Policy.UniqueID);
                if (allowDesc && e.DescLower.Contains(qLower))
                    return true;
            }
            if (_searchInComments)
            {
                if (
                    (
                        _compComments.TryGetValue(e.Policy.UniqueID, out var c1)
                        && !string.IsNullOrEmpty(c1)
                        && SearchText.Normalize(c1).Contains(qLower)
                    )
                    || (
                        _userComments.TryGetValue(e.Policy.UniqueID, out var c2)
                        && !string.IsNullOrEmpty(c2)
                        && SearchText.Normalize(c2).Contains(qLower)
                    )
                )
                    return true;
            }
            if (_searchInRegistryKey || _searchInRegistryValue)
            {
                if (
                    _searchInRegistryKey
                    && Services.RegistrySearch.SearchRegistry(e.Policy, qLower, string.Empty, true)
                )
                    return true;
                if (
                    _searchInRegistryValue
                    && Services.RegistrySearch.SearchRegistryValueNameOnly(e.Policy, qLower, true)
                )
                    return true;
            }
            return false;
        }

        private void BindSequenceEnhanced(
            IEnumerable<PolicyPlusPolicy> seq,
            FilterDecisionResult decision
        )
        {
            Func<PolicyPlusPolicy, object>? primary = null;
            if (!string.IsNullOrEmpty(_sortColumn))
            {
                primary = _sortColumn switch
                {
                    nameof(PolicyListRow.DisplayName) => p => p.DisplayName ?? string.Empty,
                    nameof(PolicyListRow.ShortId) => p =>
                    {
                        var id = p.UniqueID ?? string.Empty;
                        int i = id.LastIndexOf(':');
                        return i >= 0 && i + 1 < id.Length ? id[(i + 1)..] : id;
                    },
                    nameof(PolicyListRow.CategoryName) => p =>
                        p.Category?.DisplayName ?? string.Empty,
                    nameof(PolicyListRow.TopCategoryName) => p =>
                    {
                        var c = p.Category;
                        while (c?.Parent != null)
                            c = c.Parent;
                        return c?.DisplayName ?? string.Empty;
                    },
                    nameof(PolicyListRow.CategoryFullPath) => p =>
                    {
                        var parts = new List<string>();
                        var c = p.Category;
                        while (c != null)
                        {
                            parts.Add(c.DisplayName ?? string.Empty);
                            c = c.Parent;
                        }
                        parts.Reverse();
                        return string.Join(" / ", parts);
                    },
                    nameof(PolicyListRow.AppliesText) => p =>
                        p.RawPolicy.Section switch
                        {
                            AdmxPolicySection.Machine => 1,
                            AdmxPolicySection.User => 2,
                            _ => 0,
                        },
                    nameof(PolicyListRow.SupportedText) => p =>
                        p.SupportedOn?.DisplayName ?? string.Empty,
                    _ => null,
                };
            }
            var ordered =
                primary != null
                    ? (
                        _sortDirection
                        == CommunityToolkit.WinUI.UI.Controls.DataGridSortDirection.Descending
                            ? seq.OrderByDescending(primary)
                                .ThenBy(p => p.DisplayName)
                                .ThenBy(p => p.UniqueID)
                                .ToList()
                            : seq.OrderBy(primary)
                                .ThenBy(p => p.DisplayName)
                                .ThenBy(p => p.UniqueID)
                                .ToList()
                    )
                    : seq.OrderBy(p => p.DisplayName).ThenBy(p => p.UniqueID).ToList();
            if (decision.Limit.HasValue && ordered.Count > decision.Limit.Value)
                ordered = ordered.Take(decision.Limit.Value).ToList();
            _visiblePolicies = ordered.ToList();
            bool computeStatesNow = !_navTyping || _visiblePolicies.Count <= LargeResultThreshold;
            var srcCtx = PolicySourceAccessor.Acquire();
            bool forceProvideSources = srcCtx.Mode == PolicySourceMode.CustomPol;
            _rowByPolicyId.Clear();
            var compSrc = (computeStatesNow || forceProvideSources) ? srcCtx.Comp : null;
            var userSrc = (computeStatesNow || forceProvideSources) ? srcCtx.User : null;
            var rows = new List<object>();
            try
            {
                if (decision.ShowSubcategoryHeaders && _selectedCategory != null)
                    foreach (var child in _selectedCategory.Children.OrderBy(c => c.DisplayName))
                        rows.Add(PolicyListRow.FromCategory(child));
            }
            catch { }
            rows.AddRange(
                ordered.Select(p => (object)PolicyListRow.FromPolicy(p, compSrc, userSrc))
            );
            foreach (var obj in rows)
                if (obj is PolicyListRow r && r.Policy != null)
                    _rowByPolicyId[r.Policy.UniqueID] = r;
            PolicyList.ItemsSource = rows;
            PolicyCount.Text = $"{_visiblePolicies.Count} / {_allPolicies.Count} policies";
            TryRestoreSelectionAsync(rows);
            MaybePushCurrentState();
        }

        private List<PolicyPlusPolicy> MatchPolicies(
            string query,
            IEnumerable<PolicyPlusPolicy> baseSeq,
            out HashSet<string> allowedSet
        )
        {
            var qLower = SearchText.Normalize(query);
            allowedSet = new HashSet<string>(
                baseSeq.Select(p => p.UniqueID),
                StringComparer.OrdinalIgnoreCase
            );
            var matched = new List<PolicyPlusPolicy>();
            HashSet<string>? baseCandidates = GetTextCandidates(qLower);
            HashSet<string>? descCandidates = null;
            if (_searchInDescription)
            {
                EnsureDescIndex();
                descCandidates = _descIndex.TryQuery(qLower);
            }
            HashSet<string> scanSet = allowedSet;
            if (baseCandidates != null && baseCandidates.Count > 0)
            {
                scanSet = new HashSet<string>(allowedSet, StringComparer.OrdinalIgnoreCase);
                scanSet.IntersectWith(baseCandidates);
                if (descCandidates != null && descCandidates.Count > 0)
                {
                    var tmp = new HashSet<string>(scanSet, StringComparer.OrdinalIgnoreCase);
                    tmp.UnionWith(descCandidates);
                    tmp.IntersectWith(allowedSet);
                    scanSet = tmp;
                }
            }
            else if (descCandidates != null && descCandidates.Count > 0)
            {
                scanSet = new HashSet<string>(allowedSet, StringComparer.OrdinalIgnoreCase);
                scanSet.IntersectWith(descCandidates);
            }
            bool smallSubset = scanSet.Count > 0 && scanSet.Count < (_allPolicies.Count / 2);
            if (smallSubset)
            {
                foreach (var id in scanSet)
                    if (
                        _searchIndexById.TryGetValue(id, out var e)
                        && PolicyMatchesQuery(e, query, qLower, descCandidates)
                    )
                        matched.Add(e.Policy);
            }
            else
            {
                foreach (var e in _searchIndex)
                    if (
                        scanSet.Contains(e.Policy.UniqueID)
                        && PolicyMatchesQuery(e, query, qLower, descCandidates)
                    )
                        matched.Add(e.Policy);
            }
            return matched;
        }

        private void ScheduleFullResultBind(
            int gen,
            string q,
            List<PolicyPlusPolicy> fullMatches,
            FilterDecisionResult decision
        )
        {
            try
            {
                var timer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(PartialExpandDelayMs),
                };
                timer.Tick += (s, e) =>
                {
                    timer.Stop();
                    if (gen != _searchGeneration)
                        return;
                    var current = (SearchBox?.Text ?? string.Empty).Trim();
                    if (!string.Equals(current, q, StringComparison.Ordinal))
                        return;
                    try
                    {
                        BindSequenceEnhanced(fullMatches, decision);
                        UpdateNavButtons();
                    }
                    catch { }
                };
                timer.Start();
            }
            catch
            {
                try
                {
                    BindSequenceEnhanced(fullMatches, decision);
                    UpdateNavButtons();
                }
                catch { }
            }
        }

        private List<string> BuildSuggestions(string q, HashSet<string> allowed)
        {
            var qLower = SearchText.Normalize(q);
            var bestByName = new Dictionary<string, (int score, string name)>(
                StringComparer.OrdinalIgnoreCase
            );
            bool smallSubset = allowed.Count > 0 && allowed.Count < (_allPolicies.Count / 2);
            void Consider(
                (
                    PolicyPlusPolicy Policy,
                    string NameLower,
                    string SecondLower,
                    string IdLower,
                    string DescLower
                ) e
            )
            {
                if (!allowed.Contains(e.Policy.UniqueID))
                    return;
                int score = 0;
                if (!string.IsNullOrEmpty(qLower))
                {
                    int nameScore = ScoreMatch(e.NameLower, qLower);
                    int secondScore = string.IsNullOrEmpty(e.SecondLower)
                        ? -1000
                        : ScoreMatch(e.SecondLower, qLower);
                    int idScore = ScoreMatch(e.IdLower, qLower);
                    int descScore = _searchInDescription ? ScoreMatch(e.DescLower, qLower) : -1000;
                    if (
                        nameScore <= -1000
                        && secondScore <= -1000
                        && idScore <= -1000
                        && descScore <= -1000
                    )
                        return;
                    // Favor primary language name over second-language match when both match
                    // by weighting primary name slightly higher and second slightly lower.
                    score += Math.Max(0, nameScore) * 4; // primary name weight
                    score += Math.Max(0, secondScore) * 2; // secondary name weight
                    score += Math.Max(0, idScore) * 2;
                    score += Math.Max(0, descScore);
                }
                score += SearchRankingService.GetBoost(e.Policy.UniqueID);
                var name = e.Policy.DisplayName ?? string.Empty;
                if (string.IsNullOrEmpty(name))
                    name = e.Policy.UniqueID;
                if (bestByName.TryGetValue(name, out var cur))
                {
                    if (score > cur.score)
                        bestByName[name] = (score, name);
                }
                else
                    bestByName[name] = (score, name);
            }
            if (smallSubset)
            {
                foreach (var id in allowed)
                    if (_searchIndexById.TryGetValue(id, out var entry))
                        Consider(entry);
            }
            else
                foreach (var e in _searchIndex)
                    Consider(e);
            if (bestByName.Count == 0 && string.IsNullOrEmpty(qLower))
            {
                foreach (var id in allowed)
                    if (_searchIndexById.TryGetValue(id, out var e))
                    {
                        int score = SearchRankingService.GetBoost(id);
                        var name = e.Policy.DisplayName ?? string.Empty;
                        if (bestByName.TryGetValue(name, out var cur))
                        {
                            if (score > cur.score)
                                bestByName[name] = (score, name);
                        }
                        else
                            bestByName[name] = (score, name);
                    }
            }
            return bestByName
                .Values.OrderByDescending(v => v.score)
                .ThenBy(v => v.name, StringComparer.InvariantCultureIgnoreCase)
                .Take(10)
                .Select(v => v.name)
                .ToList();
        }

        private void RunAsyncSearchAndBind(string q)
        {
            _searchDebounceCts?.Cancel();
            _searchDebounceCts = new System.Threading.CancellationTokenSource();
            var token = _searchDebounceCts.Token;
            int gen = Interlocked.Increment(ref _searchGeneration);
            try
            {
                if (SearchSpinner != null)
                {
                    SearchSpinner.Visibility = Visibility.Visible;
                    SearchSpinner.IsActive = true;
                }
            }
            catch { }
            var applies = _appliesFilter;
            var category = _selectedCategory;
            var configuredOnly = _configuredOnly;
            var ctx = PolicySourceAccessor.Acquire();
            var comp = ctx.Comp;
            var user = ctx.User;
            var decision = EvaluateDecision(q);
            _ = System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    await System.Threading.Tasks.Task.Delay(SearchInitialDelayMs, token);
                }
                catch
                {
                    return;
                }
                if (token.IsCancellationRequested)
                {
                    Finish();
                    return;
                }
                var snap = new FilterSnapshot(
                    applies,
                    category,
                    decision.IncludeSubcategoryPolicies,
                    configuredOnly,
                    comp,
                    user
                );
                List<PolicyPlusPolicy> matches;
                List<string> suggestions;
                try
                {
                    // Try cache-backed search first (if enabled)
                    var baseSeq = BaseSequenceForFilters(snap);
                    baseSeq = ApplyBookmarkFilterIfNeeded(baseSeq);
                    var allowedSet = new HashSet<string>(
                        baseSeq.Select(p => p.UniqueID),
                        StringComparer.OrdinalIgnoreCase
                    );

                    List<PolicyPlusPolicy> cacheMatches = new();
                    List<string> cacheSuggestions = new();
                    try
                    {
                        var st = SettingsService.Instance.LoadSettings();
                        if ((st.AdmxCacheEnabled ?? true) == false)
                            throw new InvalidOperationException("ADMX cache disabled");
                        string primary = !string.IsNullOrWhiteSpace(st.Language)
                            ? st.Language!
                            : System.Globalization.CultureInfo.CurrentUICulture.Name;
                        // Prefer primary for both matching and display; fall back to second, then en-US if enabled.
                        var tryLangs = new List<string>(3);
                        tryLangs.Add(primary);
                        if (
                            st.SecondLanguageEnabled == true
                            && !string.IsNullOrWhiteSpace(st.SecondLanguage)
                        )
                            tryLangs.Add(st.SecondLanguage!);
                        if (st.PrimaryLanguageFallbackEnabled == true)
                            tryLangs.Add("en-US");

                        IReadOnlyList<PolicyPlusCore.Core.PolicyHit>? hits = null;
                        foreach (var lang in tryLangs.Distinct(StringComparer.OrdinalIgnoreCase))
                        {
                            var fields = SearchFields.None;
                            if (_searchInName)
                                fields |= SearchFields.Name;
                            if (_searchInId)
                                fields |= SearchFields.Id;
                            if (_searchInRegistryKey || _searchInRegistryValue)
                                fields |= SearchFields.Registry;
                            if (_searchInDescription)
                                fields |= SearchFields.Description;

                            if (fields == SearchFields.None)
                            {
                                hits = Array.Empty<PolicyPlusCore.Core.PolicyHit>();
                                continue;
                            }

                            hits = await AdmxCacheHostService
                                .Instance.Cache.SearchAsync(q, lang, fields, 300, token)
                                .ConfigureAwait(false);
                            if (hits != null && hits.Count > 0)
                                break;
                        }
                        if (hits != null && hits.Count > 0)
                        {
                            // Map to policies and filter by current allowed set
                            var byId = _allPolicies.ToDictionary(
                                p => p.UniqueID,
                                StringComparer.OrdinalIgnoreCase
                            );
                            var seenUnique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            foreach (var h in hits)
                            {
                                var uid = h.UniqueId;
                                if (string.IsNullOrEmpty(uid))
                                    continue;
                                if (!allowedSet.Contains(uid))
                                    continue;
                                if (!byId.TryGetValue(uid, out var pol))
                                    continue;
                                if (seenUnique.Add(uid))
                                    cacheMatches.Add(pol);
                            }
                            // Suggestions from top names. Prefer current policy DisplayName (primary language)
                            // and fall back to the hit's localized DisplayName when unavailable.
                            cacheSuggestions = hits.Take(10)
                                .Select(h =>
                                {
                                    if (
                                        !string.IsNullOrEmpty(h.UniqueId)
                                        && byId.TryGetValue(h.UniqueId, out var pol)
                                    )
                                    {
                                        var n = pol.DisplayName ?? string.Empty;
                                        if (!string.IsNullOrWhiteSpace(n))
                                            return n;
                                    }
                                    return h.DisplayName ?? string.Empty;
                                })
                                .Where(s => !string.IsNullOrWhiteSpace(s))
                                .Distinct(StringComparer.OrdinalIgnoreCase)
                                .ToList();
                        }
                    }
                    catch { }

                    if (cacheMatches.Count > 0)
                    {
                        matches = cacheMatches;
                        suggestions =
                            cacheSuggestions.Count > 0
                                ? cacheSuggestions
                                : BuildSuggestions(q, allowedSet);
                    }
                    else
                    {
                        // Fallback to in-memory search
                        matches = MatchPolicies(q, baseSeq, out var allowed2);
                        suggestions = BuildSuggestions(q, allowed2);
                    }
                }
                catch
                {
                    suggestions = new();
                    matches = new();
                }
                if (token.IsCancellationRequested)
                {
                    Finish();
                    return;
                }
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (token.IsCancellationRequested || gen != _searchGeneration)
                    {
                        Finish();
                        return;
                    }
                    try
                    {
                        // Hide suggestions when 0 or 1 item to reduce distraction
                        bool showSuggestions = suggestions != null && suggestions.Count > 1;
                        // No forced-close behavior; showSuggestions follows computed value.
                        SearchBox.ItemsSource = showSuggestions
                            ? suggestions
                            : Array.Empty<string>();
                        try
                        {
                            SearchBox.IsSuggestionListOpen = showSuggestions;
                        }
                        catch { }
                        // _forceCloseSuggestionsOnce removed
                        if (_navTyping && matches.Count > LargeResultThreshold)
                        {
                            var partial = matches.Take(LargeResultThreshold).ToList();
                            BindSequenceEnhanced(partial, decision);
                            UpdateNavButtons();
                            ScheduleFullResultBind(gen, q, matches, decision);
                        }
                        else
                        {
                            BindSequenceEnhanced(matches, decision);
                            UpdateNavButtons();
                        }
                    }
                    finally
                    {
                        Finish();
                    }
                });
            });
            void Finish()
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    try
                    {
                        if (SearchSpinner != null)
                        {
                            SearchSpinner.IsActive = false;
                            SearchSpinner.Visibility = Visibility.Collapsed;
                        }
                    }
                    catch { }
                });
            }
        }

        private void RunAsyncFilterAndBind()
        {
            _searchDebounceCts?.Cancel();
            _searchDebounceCts = new System.Threading.CancellationTokenSource();
            var token = _searchDebounceCts.Token;
            try
            {
                PreserveScrollPosition();
            }
            catch { }
            try
            {
                if (SearchSpinner != null)
                {
                    SearchSpinner.Visibility = Visibility.Visible;
                    SearchSpinner.IsActive = true;
                }
            }
            catch { }
            var applies = _appliesFilter;
            var category = _selectedCategory;
            var configuredOnly = _configuredOnly;
            var ctx = PolicySourceAccessor.Acquire();
            var comp = ctx.Comp;
            var user = ctx.User;
            var decision = EvaluateDecision();
            _ = System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    await System.Threading.Tasks.Task.Delay(60, token);
                }
                catch
                {
                    return;
                }
                if (token.IsCancellationRequested)
                {
                    Finish();
                    return;
                }
                List<PolicyPlusPolicy> items;
                try
                {
                    var snap = new FilterSnapshot(
                        applies,
                        category,
                        decision.IncludeSubcategoryPolicies,
                        configuredOnly,
                        comp,
                        user
                    );
                    IEnumerable<PolicyPlusPolicy> seq = BaseSequenceForFilters(snap);
                    // Optional cache-backed prefilter by category for performance
                    try
                    {
                        var st = SettingsService.Instance.LoadSettings();
                        if (snap.Category != null && (st.AdmxCacheEnabled ?? true) == true)
                        {
                            // Build category key set respecting IncludeSubcategories
                            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            void AddKeysRecursive(PolicyPlusCategory c)
                            {
                                if (!string.IsNullOrEmpty(c.UniqueID))
                                    keys.Add(c.UniqueID);
                                if (snap.IncludeSubcategories)
                                {
                                    foreach (var ch in c.Children)
                                        AddKeysRecursive(ch);
                                }
                            }
                            AddKeysRecursive(snap.Category);

                            if (keys.Count > 0)
                            {
                                var uids = await Services
                                    .AdmxCacheHostService.Instance.Cache.GetPolicyUniqueIdsByCategoriesAsync(
                                        keys,
                                        token
                                    )
                                    .ConfigureAwait(false);
                                if (uids.Count > 0)
                                {
                                    var allowed = new HashSet<string>(
                                        uids,
                                        StringComparer.OrdinalIgnoreCase
                                    );
                                    seq = seq.Where(p => allowed.Contains(p.UniqueID));
                                }
                            }
                        }
                    }
                    catch { }
                    items = ApplyBookmarkFilterIfNeeded(seq).ToList();
                }
                catch
                {
                    items = new();
                }
                if (token.IsCancellationRequested)
                {
                    Finish();
                    return;
                }
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (token.IsCancellationRequested)
                    {
                        Finish();
                        return;
                    }
                    try
                    {
                        BindSequenceEnhanced(items, decision);
                        RestorePositionOrSelection();
                        UpdateNavButtons();
                        if (string.IsNullOrWhiteSpace(SearchBox?.Text))
                        {
                            ShowBaselineSuggestions();
                        }
                    }
                    finally
                    {
                        Finish();
                    }
                });
            });
            void Finish()
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    try
                    {
                        if (SearchSpinner != null)
                        {
                            SearchSpinner.IsActive = false;
                            SearchSpinner.Visibility = Visibility.Collapsed;
                        }
                    }
                    catch { }
                });
            }
        }

        private void RunImmediateFilterAndBind()
        {
            try
            {
                var decision = EvaluateDecision();
                var ctx = PolicySourceAccessor.Acquire();
                var seq = BaseSequenceForFilters(decision.IncludeSubcategoryPolicies);
                seq = ApplyBookmarkFilterIfNeeded(seq);
                BindSequenceEnhanced(seq, decision);
                UpdateSearchClearButtonVisibility();
                ShowBaselineSuggestions();
            }
            catch { }
        }
    }
}
