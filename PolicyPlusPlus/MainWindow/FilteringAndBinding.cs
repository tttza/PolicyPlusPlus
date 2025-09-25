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
        private Microsoft.UI.Xaml.Controls.ProgressRing? GetSearchSpinner() =>
            RootElement?.FindName("SearchSpinner") as Microsoft.UI.Xaml.Controls.ProgressRing;

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

        // Returns whether ADMX Cache is enabled in settings. When disabled, we also disable in-memory N-gram usage.
        private static bool IsAdmxCacheEnabled()
        {
            try
            {
                var s = SettingsService.Instance.LoadSettings();
                return s.AdmxCacheEnabled ?? true;
            }
            catch
            {
                return true;
            }
        }

        private void EnsureDescIndex()
        {
            // Do not use in-memory N-gram when cache is disabled (no-cache search mode)
            if (!IsAdmxCacheEnabled())
            {
                _descIndexBuilt = true; // mark as built to skip attempts
                return;
            }
            if (_descIndexBuilt)
                return;
            try
            {
                var items = _allPolicies.Select(p =>
                    (id: p.UniqueID, normalizedText: SearchText.Normalize(p.DisplayExplanation))
                );
                var start = DateTime.UtcNow;
                _descIndex.Build(items);
                _descIndexBuilt = true;
                Log.Info(
                    "MainFilter",
                    $"DescIndex built count={_allPolicies.Count} ms={(int)(DateTime.UtcNow - start).TotalMilliseconds}"
                );
            }
            catch (Exception ex)
            {
                Log.Error("MainFilter", "EnsureDescIndex build failed", ex);
            }
        }

        private void EnsureNameSecondIdIndexes()
        {
            // Do not use in-memory N-gram when cache is disabled (no-cache search mode)
            if (!IsAdmxCacheEnabled())
            {
                _nameIndexBuilt = true;
                _secondIndexBuilt = true;
                _idIndexBuilt = true;
                return;
            }
            if (!_nameIndexBuilt)
            {
                try
                {
                    var items = _allPolicies.Select(p =>
                        (id: p.UniqueID, normalizedText: SearchText.Normalize(p.DisplayName))
                    );
                    var start = DateTime.UtcNow;
                    _nameIndex.Build(items);
                    _nameIndexBuilt = true;
                    Log.Info(
                        "MainFilter",
                        $"NameIndex built count={_allPolicies.Count} ms={(int)(DateTime.UtcNow - start).TotalMilliseconds}"
                    );
                }
                catch (Exception ex)
                {
                    Log.Error("MainFilter", "Name index build failed", ex);
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
                            var start2 = DateTime.UtcNow;
                            _secondIndex.Build(items);
                            _secondIndexBuilt = true;
                            Log.Info(
                                "MainFilter",
                                $"SecondIndex built lang={secondLang} count={_allPolicies.Count} ms={(int)(DateTime.UtcNow - start2).TotalMilliseconds}"
                            );
                        }
                        catch (Exception ex)
                        {
                            Log.Error("MainFilter", "Second language index build failed", ex);
                        }
                    }
                    else
                    {
                        _secondIndexBuilt = true; // no second language active
                        Log.Debug("MainFilter", "SecondIndex skipped (disabled or same language)");
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
                    var items = _allPolicies.Select(p =>
                        (id: p.UniqueID, normalizedText: SearchText.Normalize(p.UniqueID))
                    );
                    var start = DateTime.UtcNow;
                    _idIndex.Build(items);
                    _idIndexBuilt = true;
                    Log.Info(
                        "MainFilter",
                        $"IdIndex built count={_allPolicies.Count} ms={(int)(DateTime.UtcNow - start).TotalMilliseconds}"
                    );
                }
                catch (Exception ex)
                {
                    Log.Error("MainFilter", "ID index build failed", ex);
                }
            }
        }

        private HashSet<string>? GetTextCandidates(string qLower)
        {
            // No-cache mode: do not use N-gram candidates at all
            if (!IsAdmxCacheEnabled())
                return null;
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

        // Check a single token across enabled fields (used in AND mode)
        private bool PolicyMatchesToken(
            (
                PolicyPlusPolicy Policy,
                string NameLower,
                string SecondLower,
                string IdLower,
                string DescLower
            ) e,
            string token,
            HashSet<string>? descCandidates = null
        )
        {
            if (_searchInName)
            {
                if (e.NameLower.Contains(token))
                    return true;
                if (!string.IsNullOrEmpty(e.SecondLower) && e.SecondLower.Contains(token))
                    return true;
            }
            if (_searchInId && e.IdLower.Contains(token))
                return true;
            if (_searchInDescription)
            {
                bool allowDesc =
                    descCandidates == null || descCandidates.Contains(e.Policy.UniqueID);
                if (allowDesc && e.DescLower.Contains(token))
                    return true;
            }
            if (_searchInComments)
            {
                if (
                    (
                        _compComments.TryGetValue(e.Policy.UniqueID, out var c1)
                        && !string.IsNullOrEmpty(c1)
                        && SearchText.Normalize(c1).Contains(token)
                    )
                    || (
                        _userComments.TryGetValue(e.Policy.UniqueID, out var c2)
                        && !string.IsNullOrEmpty(c2)
                        && SearchText.Normalize(c2).Contains(token)
                    )
                )
                    return true;
            }
            if (_searchInRegistryKey || _searchInRegistryValue)
            {
                if (
                    _searchInRegistryKey
                    && Services.RegistrySearch.SearchRegistry(e.Policy, token, string.Empty, true)
                )
                    return true;
                if (
                    _searchInRegistryValue
                    && Services.RegistrySearch.SearchRegistryValueNameOnly(e.Policy, token, true)
                )
                    return true;
            }
            return false;
        }

        private void BindSequenceEnhanced(
            IEnumerable<PolicyPlusPolicy> seq,
            FilterDecisionResult decision,
            bool forceComputeStates = false
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
            bool computeStatesNow =
                forceComputeStates
                || _forceComputeStatesOnce
                || !_navTyping
                || _visiblePolicies.Count <= LargeResultThreshold;
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
            if (_forceComputeStatesOnce)
                _forceComputeStatesOnce = false; // consume flag
        }

        private List<PolicyPlusPolicy> MatchPolicies(
            string query,
            IEnumerable<PolicyPlusPolicy> baseSeq,
            out HashSet<string> allowedSet
        )
        {
            var swMatch = System.Diagnostics.Stopwatch.StartNew();
            var qLower = SearchText.Normalize(query);
            allowedSet = new HashSet<string>(
                baseSeq.Select(p => p.UniqueID),
                StringComparer.OrdinalIgnoreCase
            );
            // AND mode: split by spaces (half/full width unified by Normalize)
            if (_useAndModeFlag)
            {
                var tokens = qLower.Split(
                    new[] { ' ' },
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
                );
                if (tokens.Length > 1)
                {
                    HashSet<string>? descCandidatesAnd = null;
                    if (_searchInDescription)
                    {
                        EnsureDescIndex();
                        // For AND semantics, we don't pre-intersect description; we'll check per item
                        // but we can still get candidates for each token when available.
                    }

                    // Intersect text candidates for each token to minimize scan
                    HashSet<string>? intersectAnd = null;
                    foreach (var t in tokens)
                    {
                        var cand = GetTextCandidates(t);
                        if (cand == null || cand.Count == 0)
                            continue;
                        if (intersectAnd == null)
                            intersectAnd = new HashSet<string>(
                                cand,
                                StringComparer.OrdinalIgnoreCase
                            );
                        else
                            intersectAnd.IntersectWith(cand);
                        if (intersectAnd.Count == 0)
                            break;
                    }

                    var scanSetAnd =
                        (intersectAnd != null && intersectAnd.Count > 0)
                            ? new HashSet<string>(
                                intersectAnd.Where(allowedSet.Contains),
                                StringComparer.OrdinalIgnoreCase
                            )
                            : allowedSet;

                    var matchedAnd = new List<PolicyPlusPolicy>();
                    bool smallSubsetAnd =
                        scanSetAnd.Count > 0 && scanSetAnd.Count < (_allPolicies.Count / 2);
                    if (smallSubsetAnd)
                    {
                        foreach (var id in scanSetAnd)
                            if (
                                _searchIndexById.TryGetValue(id, out var e)
                                && tokens.All(t => PolicyMatchesToken(e, t, descCandidatesAnd))
                            )
                                matchedAnd.Add(e.Policy);
                    }
                    else
                    {
                        foreach (var e in _searchIndex)
                            if (
                                scanSetAnd.Contains(e.Policy.UniqueID)
                                && tokens.All(t => PolicyMatchesToken(e, t, descCandidatesAnd))
                            )
                                matchedAnd.Add(e.Policy);
                    }
                    return matchedAnd;
                }
            }
            var matched = new List<PolicyPlusPolicy>();
            HashSet<string>? baseCandidates = GetTextCandidates(qLower);
            HashSet<string>? descCandidates = null;
            if (_searchInDescription)
            {
                if (IsAdmxCacheEnabled())
                {
                    EnsureDescIndex();
                    descCandidates = _descIndex.TryQuery(qLower);
                }
                else
                {
                    // No-cache mode: do not prefilter by N-gram; allow per-item description match
                    descCandidates = null;
                }
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
            swMatch.Stop();
            Log.Trace(
                "MainSearch",
                $"MatchPolicies ms={swMatch.ElapsedMilliseconds} qLen={query.Length} matched={matched.Count}"
            );
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
            var swSuggest = System.Diagnostics.Stopwatch.StartNew();
            var qLower = SearchText.Normalize(q);
            // Tokenize when AND mode is enabled
            string[] tokens = Array.Empty<string>();
            if (_useAndModeFlag && !string.IsNullOrEmpty(qLower))
                tokens = qLower.Split(
                    new[] { ' ' },
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
                );
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
                    if (_useAndModeFlag && tokens.Length > 0)
                    {
                        // AND suggestions: require each token to match at least one enabled field.
                        foreach (var t in tokens)
                        {
                            int tokenBest = int.MinValue;
                            if (_searchInName)
                            {
                                tokenBest = Math.Max(tokenBest, ScoreMatch(e.NameLower, t));
                                if (!string.IsNullOrEmpty(e.SecondLower))
                                    tokenBest = Math.Max(tokenBest, ScoreMatch(e.SecondLower, t));
                            }
                            if (_searchInId)
                                tokenBest = Math.Max(tokenBest, ScoreMatch(e.IdLower, t));
                            if (_searchInDescription)
                                tokenBest = Math.Max(tokenBest, ScoreMatch(e.DescLower, t));
                            // comments/registry are expensive; keep suggestions focused to text fields
                            if (tokenBest <= -1000)
                                return; // this token didn't match any text field -> exclude from suggestions
                            // weight primary name more by scaling positive tokenBest observed via name
                            score += Math.Max(0, tokenBest);
                        }
                        // Slight boost when name starts with the first token (common UX expectation)
                        if (
                            !string.IsNullOrEmpty(e.NameLower)
                            && e.NameLower.StartsWith(tokens[0], StringComparison.Ordinal)
                        )
                            score += 5;
                    }
                    else
                    {
                        // Default (OR-like) suggestion model for single token
                        int nameScore = ScoreMatch(e.NameLower, qLower);
                        int secondScore = string.IsNullOrEmpty(e.SecondLower)
                            ? -1000
                            : ScoreMatch(e.SecondLower, qLower);
                        int idScore = ScoreMatch(e.IdLower, qLower);
                        int descScore = _searchInDescription
                            ? ScoreMatch(e.DescLower, qLower)
                            : -1000;
                        if (
                            nameScore <= -1000
                            && secondScore <= -1000
                            && idScore <= -1000
                            && descScore <= -1000
                        )
                            return;
                        score += Math.Max(0, nameScore) * 4; // primary name weight
                        score += Math.Max(0, secondScore) * 2; // secondary name weight
                        score += Math.Max(0, idScore) * 2;
                        score += Math.Max(0, descScore);
                    }
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
            var result = bestByName
                .Values.OrderByDescending(v => v.score)
                .ThenBy(v => v.name, StringComparer.InvariantCultureIgnoreCase)
                .Take(10)
                .Select(v => v.name)
                .ToList();
            swSuggest.Stop();
            Log.Trace(
                "MainSearch",
                $"BuildSuggestions ms={swSuggest.ElapsedMilliseconds} qLen={q.Length} cand={bestByName.Count}"
            );
            return result;
        }

        private void RunAsyncSearchAndBind(string q)
        {
            // Normalize to empty string to satisfy nullable analysis.
            q = q ?? string.Empty;
            var swTotal = System.Diagnostics.Stopwatch.StartNew();
            Log.Trace("MainSearch", $"RunAsyncSearchAndBind start qLen={q.Length}");
            _searchDebounceCts?.Cancel();
            _searchDebounceCts = new System.Threading.CancellationTokenSource();
            var token = _searchDebounceCts.Token;
            int gen = Interlocked.Increment(ref _searchGeneration);
            try
            {
                var spinner = GetSearchSpinner();
                if (spinner != null)
                {
                    spinner.Visibility = Visibility.Visible;
                    spinner.IsActive = true;
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
                var swCompute = System.Diagnostics.Stopwatch.StartNew();
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

                    // In AND mode with multi-token query, skip cache for consistent semantics
                    bool skipCacheForAnd =
                        _useAndModeFlag && (q.IndexOf(' ') >= 0 || q.IndexOf('\u3000') >= 0);

                    List<PolicyPlusPolicy> cacheMatches = new();
                    List<string> cacheSuggestions = new();
                    try
                    {
                        if (skipCacheForAnd)
                            throw new InvalidOperationException("skip cache for AND");
                        var st = SettingsService.Instance.LoadSettings();
                        if ((st.AdmxCacheEnabled ?? true) == false)
                            throw new InvalidOperationException("ADMX cache disabled");
                        if (Services.AdmxCacheHostService.Instance.IsRebuilding)
                            throw new InvalidOperationException("ADMX cache rebuilding");
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
                        // Fallback to in-memory search (AND mode always comes here when multi-token)
                        matches = MatchPolicies(q, baseSeq, out var allowed2);
                        suggestions = BuildSuggestions(q, allowed2);
                    }
                }
                catch
                {
                    suggestions = new();
                    matches = new();
                }
                swCompute.Stop();
                if (token.IsCancellationRequested)
                {
                    Finish();
                    return;
                }
                DispatcherQueue.TryEnqueue(() =>
                {
                    var swBind = System.Diagnostics.Stopwatch.StartNew();
                    if (token.IsCancellationRequested || gen != _searchGeneration)
                    {
                        Finish();
                        return;
                    }
                    try
                    {
                        // Hide suggestions when 0 or 1 item to reduce distraction
                        bool showSuggestions = suggestions != null && suggestions.Count > 1;
                        // Do not open suggestions unless the search box already has focus to avoid focus shifts on category changes.
                        bool boxHasFocus = false;
                        try
                        {
                            var sbFocus = GetSearchBox();
                            boxHasFocus =
                                sbFocus != null && sbFocus.FocusState != FocusState.Unfocused;
                        }
                        catch { }
                        bool shouldOpenSuggestions = showSuggestions && boxHasFocus;
                        var sb = GetSearchBox();
                        if (sb != null)
                        {
                            IEnumerable<string> itemsToShow;
                            if (shouldOpenSuggestions && suggestions != null)
                                itemsToShow = suggestions;
                            else
                                itemsToShow = Array.Empty<string>();
                            sb.ItemsSource = itemsToShow;
                            try
                            {
                                sb.IsSuggestionListOpen = shouldOpenSuggestions;
                            }
                            catch { }
                        }
                        // _forceCloseSuggestionsOnce removed
                        if (_navTyping && matches.Count > LargeResultThreshold)
                        {
                            var partial = matches.Take(LargeResultThreshold).ToList();
                            BindSequenceEnhanced(partial, decision, forceComputeStates: false);
                            UpdateNavButtons();
                            Log.Debug(
                                "MainSearch",
                                $"Partial bind count={partial.Count} total={matches.Count}"
                            );
                            ScheduleFullResultBind(gen, q, matches, decision);
                        }
                        else
                        {
                            BindSequenceEnhanced(matches, decision, forceComputeStates: false);
                            UpdateNavButtons();
                            Log.Debug("MainSearch", $"Full bind count={matches.Count}");
                        }
                    }
                    finally
                    {
                        swBind.Stop();
                        Log.Debug(
                            "MainSearch",
                            $"SearchPerf qLen={q.Length} computeMs={swCompute.ElapsedMilliseconds} bindMs={swBind.ElapsedMilliseconds} totalMs={swTotal.ElapsedMilliseconds}"
                        );
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
                        var spinF = GetSearchSpinner();
                        if (spinF != null)
                        {
                            spinF.IsActive = false;
                            spinF.Visibility = Visibility.Collapsed;
                        }
                        swTotal.Stop();
                        Log.Trace(
                            "MainSearch",
                            $"RunAsyncSearchAndBind finish totalMs={swTotal.ElapsedMilliseconds}"
                        );
                    }
                    catch { }
                });
            }
        }

        private void RunAsyncFilterAndBind(bool showBaselineOnEmpty = true)
        {
            var swTotal = System.Diagnostics.Stopwatch.StartNew();
            Log.Trace("MainSearch", $"RunAsyncFilterAndBind start baseline={showBaselineOnEmpty}");
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
                var spinner2 = GetSearchSpinner();
                if (spinner2 != null)
                {
                    spinner2.Visibility = Visibility.Visible;
                    spinner2.IsActive = true;
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
                var swCompute = System.Diagnostics.Stopwatch.StartNew();
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
                        if (
                            snap.Category != null
                            && (st.AdmxCacheEnabled ?? true) == true
                            && !Services.AdmxCacheHostService.Instance.IsRebuilding
                        )
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
                swCompute.Stop();
                if (token.IsCancellationRequested)
                {
                    Finish();
                    return;
                }
                DispatcherQueue.TryEnqueue(() =>
                {
                    var swBind = System.Diagnostics.Stopwatch.StartNew();
                    if (token.IsCancellationRequested)
                    {
                        Finish();
                        return;
                    }
                    try
                    {
                        BindSequenceEnhanced(
                            items,
                            decision,
                            forceComputeStates: _forceComputeStatesOnce
                        );
                        RestorePositionOrSelection();
                        UpdateNavButtons();
                        var sb2 = GetSearchBox();
                        if (showBaselineOnEmpty && string.IsNullOrWhiteSpace(sb2?.Text))
                        {
                            try
                            {
                                // Only show baseline suggestions if the search box is already focused.
                                ShowBaselineSuggestions(onlyIfFocused: true);
                            }
                            catch { }
                        }
                        Log.Debug(
                            "MainSearch",
                            $"FilterPerf count={items.Count} computeMs={swCompute.ElapsedMilliseconds} bindMs={swBind.ElapsedMilliseconds} totalMs={swTotal.ElapsedMilliseconds}"
                        );
                    }
                    finally
                    {
                        swBind.Stop();
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
                        var spinF2 = GetSearchSpinner();
                        if (spinF2 != null)
                        {
                            spinF2.IsActive = false;
                            spinF2.Visibility = Visibility.Collapsed;
                        }
                        swTotal.Stop();
                        Log.Trace(
                            "MainSearch",
                            $"RunAsyncFilterAndBind finish totalMs={swTotal.ElapsedMilliseconds}"
                        );
                    }
                    catch { }
                });
            }
        }

        private void RunImmediateFilterAndBind(bool showBaselineOnEmpty = true)
        {
            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                Log.Trace(
                    "MainSearch",
                    $"RunImmediateFilterAndBind start baseline={showBaselineOnEmpty}"
                );
                var decision = EvaluateDecision();
                var ctx = PolicySourceAccessor.Acquire();
                var seq = BaseSequenceForFilters(decision.IncludeSubcategoryPolicies);
                seq = ApplyBookmarkFilterIfNeeded(seq);
                BindSequenceEnhanced(seq, decision, forceComputeStates: _forceComputeStatesOnce);
                UpdateSearchClearButtonVisibility();
                if (showBaselineOnEmpty)
                    // Only open baseline suggestions if the search box is already focused.
                    ShowBaselineSuggestions(onlyIfFocused: true);
                sw.Stop();
                Log.Debug(
                    "MainSearch",
                    $"ImmediateFilterPerf count={_visiblePolicies?.Count} totalMs={sw.ElapsedMilliseconds}"
                );
            }
            catch { }
        }
    }
}
