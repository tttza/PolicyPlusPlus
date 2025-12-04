// Filtering and binding logic (on-demand sources)
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls; // for TextBlock
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
        // Pre-filter counts to compute potential gains if a filter is disabled (Config A scope).
        private int _preConfiguredFilterCount = -1;
        private int _preBookmarksFilterCount = -1;
        private int _deltaConfigured = 0;
        private int _deltaBookmarks = 0;
        private int _deltaCategory = 0; // count if category filter cleared
        private bool _hasAnyBookmarks = true; // set by ApplyBookmarkFilterIfNeeded
        private bool _hasAnyConfiguredInScope = true; // set by ApplyConfiguredFilterIfNeeded

        private Microsoft.UI.Xaml.Controls.ProgressRing? GetSearchSpinner() =>
            RootElement?.FindName("SearchSpinner") as Microsoft.UI.Xaml.Controls.ProgressRing;

        private const int LargeResultThreshold = 200;
        private const int AndModeCandidateProbeLimit = 800;
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
            // Take an immutable snapshot of _allPolicies to avoid concurrent modification exceptions
            // while indexes are being built (some background fill operations may still populate missing localized strings).
            var snapshot = _allPolicies.ToArray();
            if (!_nameIndexBuilt)
            {
                try
                {
                    var items = snapshot.Select(p =>
                        (id: p.UniqueID, normalizedText: SearchText.Normalize(p.DisplayName))
                    );
                    var start = DateTime.UtcNow;
                    _nameIndex.Build(items);
                    _nameIndexBuilt = true; // mark after successful build
                    Log.Info(
                        "MainFilter",
                        $"NameIndex built count={snapshot.Length} ms={(int)(DateTime.UtcNow - start).TotalMilliseconds}"
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
                            var items = snapshot.Select(p =>
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
                                $"SecondIndex built lang={secondLang} count={snapshot.Length} ms={(int)(DateTime.UtcNow - start2).TotalMilliseconds}"
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
                    var items = snapshot.Select(p =>
                        (id: p.UniqueID, normalizedText: SearchText.Normalize(p.UniqueID))
                    );
                    var start = DateTime.UtcNow;
                    _idIndex.Build(items);
                    _idIndexBuilt = true;
                    Log.Info(
                        "MainFilter",
                        $"IdIndex built count={snapshot.Length} ms={(int)(DateTime.UtcNow - start).TotalMilliseconds}"
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
            // Normalize Japanese full-width spaces to ASCII space once here to align with tokenization.
            if (qLower.IndexOf('\u3000') >= 0)
                qLower = qLower.Replace('\u3000', ' ');
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

        // Additional helper to extract AND-mode tokens for CJK phrases split by spaces, so that
        // a query like "拡張 インストール" (user typed space) produces two logical tokens instead of relying solely on 2-gram overlap.
        private static List<string> ExtractExplicitSpaceTokens(string input)
        {
            var list = new List<string>();
            if (string.IsNullOrWhiteSpace(input))
                return list;
            // Collapse multiple spaces (already mostly done upstream) and split.
            var parts = input.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
            );
            foreach (var p in parts)
            {
                // Only consider CJK-heavy tokens or those longer than 1 char; skip plain ASCII 1-char which adds noise.
                if (p.Length > 1 || p.Any(ch => ch > 0x7F))
                    list.Add(p);
            }
            return list;
        }

        private IEnumerable<PolicyPlusPolicy> BaseSequenceForFilters(bool includeSubcategories)
        {
            var ctx = PolicySourceAccessor.Acquire();
            return BaseSequenceForFilters(
                new FilterSnapshot(
                    _appliesFilter,
                    _selectedCategory,
                    includeSubcategories,
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
                IPolicySource? comp,
                IPolicySource? user
            )
            {
                Applies = applies;
                Category = category;
                IncludeSubcategories = includeSubcats;
                CompSource = comp;
                UserSource = user;
            }

            public AdmxPolicySection Applies { get; }
            public PolicyPlusCategory? Category { get; }
            public bool IncludeSubcategories { get; }
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
            return seq;
        }

        private IEnumerable<PolicyPlusPolicy> ApplyConfiguredFilterIfNeeded(
            IEnumerable<PolicyPlusPolicy> seq,
            IPolicySource? compSrc,
            IPolicySource? userSrc
        )
        {
            if (!_configuredOnly)
            {
                _preConfiguredFilterCount = -1;
                _deltaConfigured = 0;
                _hasAnyConfiguredInScope = true; // not applicable
                return seq;
            }
            // Materialize once for count + filtering pass.
            var list = seq as IList<PolicyPlusPolicy> ?? seq.ToList();
            _preConfiguredFilterCount = list.Count;
            var pending =
                PendingChangesService.Instance.Pending?.ToList() ?? new List<PendingChange>();
            if (compSrc == null && userSrc == null && pending.Count == 0)
            {
                // No sources, so configured-only degenerates to empty.
                _hasAnyConfiguredInScope = false;
                return Array.Empty<PolicyPlusPolicy>();
            }
            var compLocal = compSrc;
            var userLocal = userSrc;
            bool Predicate(PolicyPlusPolicy p)
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
                                ? (pu.DesiredState is PolicyState.Enabled or PolicyState.Disabled)
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
                            && pc2.Scope.Equals("Computer", StringComparison.OrdinalIgnoreCase)
                        );
                        effComp =
                            pc != null
                                ? (pc.DesiredState is PolicyState.Enabled or PolicyState.Disabled)
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
            }
            var any = list.Any(Predicate);
            _hasAnyConfiguredInScope = any;
            if (!any)
                return Array.Empty<PolicyPlusPolicy>();
            return list.Where(Predicate);
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
            {
                _preBookmarksFilterCount = -1;
                _deltaBookmarks = 0;
                _hasAnyBookmarks = true; // Irrelevant when filter disabled.
                return seq;
            }
            try
            {
                var list = seq as IList<PolicyPlusPolicy> ?? seq.ToList();
                _preBookmarksFilterCount = list.Count;
                var ids = BookmarkService.Instance.ActiveIds;
                _hasAnyBookmarks = ids != null && ids.Count > 0;
                if (!_hasAnyBookmarks || ids == null)
                    return Array.Empty<PolicyPlusPolicy>();
                var set = new HashSet<string>(ids!, StringComparer.OrdinalIgnoreCase);
                return list.Where(p => set.Contains(p.UniqueID));
            }
            catch
            {
                return seq;
            }
        }

        private static string NormalizeQueryInput(string? query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return string.Empty;
            return query.Trim();
        }

        private void ApplyFiltersAndBind(string query = "", PolicyPlusCategory? category = null)
        {
            if (PolicyList == null || PolicyCount == null)
                return;
            if (category != null)
                _selectedCategory = category;
            PreserveScrollPosition();
            UpdateSearchPlaceholder();
            var normalizedQuery = NormalizeQueryInput(query);
            if (!string.IsNullOrEmpty(normalizedQuery))
            {
                RebindConsideringAsync(normalizedQuery);
                return;
            }
            var decision = EvaluateDecision(normalizedQuery);
            IEnumerable<PolicyPlusPolicy> seq = BaseSequenceForFilters(
                decision.IncludeSubcategoryPolicies
            );
            // Bookmarks first so configured delta is relative to the already bookmark-filtered set.
            seq = ApplyBookmarkFilterIfNeeded(seq);
            var srcCtx = PolicySourceAccessor.Acquire();
            seq = ApplyConfiguredFilterIfNeeded(seq, srcCtx.Comp, srcCtx.User);
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
            catch (Exception ex)
            {
                Log.Debug("MainSearch", "Add subcategory headers failed: " + ex.Message);
            }
            rows.AddRange(
                ordered.Select(p => (object)PolicyListRow.FromPolicy(p, compSrc, userSrc))
            );
            foreach (var obj in rows)
                if (obj is PolicyListRow r && r.Policy != null)
                    _rowByPolicyId[r.Policy.UniqueID] = r;
            PolicyList.ItemsSource = rows;
            PolicyCount.Text = $"{_visiblePolicies.Count} / {_allPolicies.Count} policies";
            // Compute deltas after final visible count is known.
            if (
                _configuredOnly
                && _preConfiguredFilterCount >= 0
                && _preConfiguredFilterCount >= _visiblePolicies.Count
            )
                _deltaConfigured = _preConfiguredFilterCount - _visiblePolicies.Count;
            else
                _deltaConfigured = 0;
            if (
                _bookmarksOnly
                && _preBookmarksFilterCount >= 0
                && _preBookmarksFilterCount >= _visiblePolicies.Count
            )
                _deltaBookmarks = _preBookmarksFilterCount - _visiblePolicies.Count;
            else
                _deltaBookmarks = 0;
            TryRestoreSelectionAsync(rows);
            MaybePushCurrentState();
            if (_forceComputeStatesOnce)
                _forceComputeStatesOnce = false; // consume flag

            // Update empty-results hint after binding completes.
            try
            {
                string? qNow = GetSearchBox()?.Text;
                UpdateEmptyHint(qNow);
            }
            catch { }
            HandlePostSearchFocusAfterBind();
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

                    var scanSetAnd = BuildAndModeCandidateSet(tokens, allowedSet, out _);

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

        private HashSet<string> BuildAndModeCandidateSet(
            string[] tokens,
            HashSet<string> allowedSet,
            out bool usedIntersection
        )
        {
            usedIntersection = false;
            HashSet<string>? intersectAnd = null;
            foreach (var t in tokens)
            {
                var cand = GetTextCandidates(t);
                if (cand == null || cand.Count == 0)
                    continue;
                if (intersectAnd == null)
                {
                    intersectAnd = new HashSet<string>(cand, StringComparer.OrdinalIgnoreCase);
                }
                else
                {
                    intersectAnd.IntersectWith(cand);
                }
                if (intersectAnd.Count == 0)
                    break;
            }

            if (intersectAnd != null && intersectAnd.Count > 0)
            {
                usedIntersection = true;
                var filtered = new HashSet<string>(intersectAnd, StringComparer.OrdinalIgnoreCase);
                filtered.IntersectWith(allowedSet);
                return filtered;
            }

            return allowedSet;
        }

        private bool TryDetectAndModeCacheGap(
            string query,
            HashSet<string> allowedSet,
            IReadOnlyCollection<PolicyPlusPolicy> cacheMatches,
            int cacheSearchLimit,
            List<PolicyPlusPolicy> baseSeqSnapshot,
            out List<PolicyPlusPolicy>? fallbackMatches,
            out HashSet<string>? fallbackAllowed
        )
        {
            fallbackMatches = null;
            fallbackAllowed = null;
            if (!_useAndModeFlag || cacheMatches.Count == 0)
                return false;
            if (cacheMatches.Count >= cacheSearchLimit)
                return false; // cache already clipped to limit; differences are expected
            if (allowedSet.Count == 0)
                return false;

            var qLower = SearchText.Normalize(query);
            var tokens = qLower.Split(
                new[] { ' ' },
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
            );
            if (tokens.Length <= 1)
                return false;

            bool usedIntersection;
            var candidateSet = BuildAndModeCandidateSet(tokens, allowedSet, out usedIntersection);
            if (candidateSet.Count == 0)
                return false;
            if (!usedIntersection && candidateSet.Count > AndModeCandidateProbeLimit)
                return false; // would require scanning nearly entire allowed set
            if (candidateSet.Count > AndModeCandidateProbeLimit)
                return false; // avoid probing extremely large intersections

            var cacheSet = new HashSet<string>(
                cacheMatches.Select(p => p.UniqueID),
                StringComparer.OrdinalIgnoreCase
            );
            bool missing = false;
            foreach (var id in candidateSet)
            {
                if (string.IsNullOrEmpty(id))
                    continue;
                if (!cacheSet.Contains(id))
                {
                    missing = true;
                    break;
                }
            }
            if (!missing)
                return false;

            fallbackMatches = MatchPolicies(query, baseSeqSnapshot, out fallbackAllowed);
            return true;
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
                    catch (Exception ex)
                    {
                        Log.Debug(
                            "MainSearch",
                            "ScheduleFullResultBind inner bind failed: " + ex.Message
                        );
                    }
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
                catch (Exception ex2)
                {
                    Log.Debug(
                        "MainSearch",
                        "ScheduleFullResultBind fallback bind failed: " + ex2.Message
                    );
                }
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
            // Start overall stopwatch only for actual compute/bind portion (exclude debounce delay)
            System.Diagnostics.Stopwatch? swTotal = null;
            Log.Trace(
                "MainSearch",
                $"RunAsyncSearchAndBind start qLen={q.Length} cache={(IsAdmxCacheEnabled() ? "on" : "off")}"
            );
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
            catch (Exception ex)
            {
                Log.Debug("MainSearch", "Show spinner failed: " + ex.Message);
            }
            var applies = _appliesFilter;
            var category = _selectedCategory;
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
                // Begin total timing after debounce delay so reported totalMs reflects only work.
                swTotal = System.Diagnostics.Stopwatch.StartNew();
                if (token.IsCancellationRequested)
                {
                    Finish();
                    return;
                }
                var snap = new FilterSnapshot(
                    applies,
                    category,
                    decision.IncludeSubcategoryPolicies,
                    comp,
                    user
                );
                var swCompute = System.Diagnostics.Stopwatch.StartNew();
                List<PolicyPlusPolicy> matches = new();
                List<string> suggestions = new();
                try
                {
                    // Try cache-backed search first (if enabled)
                    var baseSeq = BaseSequenceForFilters(snap);
                    baseSeq = ApplyBookmarkFilterIfNeeded(baseSeq);
                    // Apply configured-only after bookmarks to capture delta metrics.
                    baseSeq = ApplyConfiguredFilterIfNeeded(baseSeq, comp, user);
                    var baseList = baseSeq as List<PolicyPlusPolicy> ?? baseSeq.ToList();
                    var allowedSet = new HashSet<string>(
                        baseList.Select(p => p.UniqueID),
                        StringComparer.OrdinalIgnoreCase
                    );

                    // Determine AND mode: user flag + multiple tokens (space or full-width space). We now fully support cache in AND mode.
                    bool andMode =
                        _useAndModeFlag && (q.IndexOf(' ') >= 0 || q.IndexOf('\u3000') >= 0);

                    List<PolicyPlusPolicy> cacheMatches = new();
                    List<string> cacheSuggestions = new();
                    bool cacheAttempted = false;
                    bool cacheSucceeded = false;
                    // Prepare search field flags outside try so diagnostics after catch can access.
                    var searchFields = SearchFields.None; // will be populated inside try
                    int cacheSearchLimit = _limitUnfilteredTo1000 ? 1000 : 5000;
                    try
                    {
                        cacheAttempted = true;
                        // AND mode also supported by cache (SQL AND implemented) so skipping is unnecessary.
                        var st = SettingsService.Instance.LoadSettings();
                        if ((st.AdmxCacheEnabled ?? true) == false)
                            throw new InvalidOperationException("ADMX cache disabled");
                        if (Services.AdmxCacheHostService.Instance.IsRebuilding)
                            throw new InvalidOperationException("ADMX cache rebuilding");
                        var slots = PolicyPlusCore.Utilities.CulturePreference.Build(
                            new PolicyPlusCore.Utilities.CulturePreference.BuildOptions(
                                Primary: string.IsNullOrWhiteSpace(st.Language)
                                    ? System.Globalization.CultureInfo.CurrentUICulture.Name
                                    : st.Language!,
                                Second: st.SecondLanguage,
                                SecondEnabled: st.SecondLanguageEnabled ?? false,
                                OsUiCulture: System.Globalization.CultureInfo.CurrentUICulture.Name,
                                EnablePrimaryFallback: st.PrimaryLanguageFallbackEnabled ?? false
                            )
                        );
                        var tryLangs = PolicyPlusCore.Utilities.CulturePreference.FlattenNames(
                            slots
                        );

                        IReadOnlyList<PolicyPlusCore.Core.PolicyHit>? hits = null;
                        if (_searchInName)
                            searchFields |= SearchFields.Name;
                        if (_searchInId)
                            searchFields |= SearchFields.Id;
                        if (_searchInRegistryKey || _searchInRegistryValue)
                            searchFields |= SearchFields.Registry;
                        if (_searchInDescription)
                            searchFields |= SearchFields.Description;
                        if (searchFields == SearchFields.None)
                        {
                            hits = Array.Empty<PolicyPlusCore.Core.PolicyHit>();
                        }
                        else
                        {
                            var orderedCultures = tryLangs; // already distinct via builder
                            hits = await AdmxCacheHostService
                                .Instance.Cache.SearchAsync(
                                    q,
                                    orderedCultures,
                                    searchFields,
                                    andMode,
                                    cacheSearchLimit,
                                    token
                                )
                                .ConfigureAwait(false);
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
                        cacheSucceeded = true; // reached end of try without throwing
                    }
                    catch (Exception ex)
                    {
                        Log.Debug("MainSearch", "cache search failed: " + ex.Message);
                    }

                    if (cacheMatches.Count > 0)
                    {
                        Log.Debug(
                            "MainSearch",
                            $"cache-hit mode={(andMode ? "AND" : "OR")} qLen={q.Length} hitCount={cacheMatches.Count}"
                        );
                        if (
                            andMode
                            && TryDetectAndModeCacheGap(
                                q,
                                allowedSet,
                                cacheMatches,
                                cacheSearchLimit,
                                baseList,
                                out var fallbackMatches,
                                out var fallbackAllowed
                            )
                        )
                        {
                            Log.Debug(
                                "MainSearch",
                                "cache-hit mismatch detected, falling back to memory AND search"
                            );
                            cacheMatches = fallbackMatches ?? cacheMatches;
                            cacheSuggestions = BuildSuggestions(q, fallbackAllowed ?? allowedSet);
                        }
                        matches = cacheMatches;
                        suggestions =
                            cacheSuggestions.Count > 0
                                ? cacheSuggestions
                                : BuildSuggestions(q, allowedSet);
                    }
                    else
                    {
                        // Diagnostic: attempt to classify why cache returned 0.
                        try
                        {
                            // Derive field flags string.
                            var sf = searchFields; // use final value
                            string fieldFlags =
                                sf == SearchFields.None
                                    ? "None"
                                    : string.Join(
                                        '|',
                                        new[]
                                        {
                                            (sf & SearchFields.Name) != 0 ? "Name" : null,
                                            (sf & SearchFields.Description) != 0 ? "Desc" : null,
                                            (sf & SearchFields.Id) != 0 ? "Id" : null,
                                            (sf & SearchFields.Registry) != 0 ? "Reg" : null,
                                        }.Where(s => s != null)!
                                    );
                            // Basic tokenization similar to core (whitespace split after strict/loose normalization)
                            var strictNorm =
                                PolicyPlusCore.Utilities.TextNormalization.NormalizeStrict(q);
                            var looseNorm =
                                PolicyPlusCore.Utilities.TextNormalization.NormalizeLoose(q);
                            string[] SplitTokensLocal(string s) =>
                                s.Split(
                                    new[] { ' ' },
                                    StringSplitOptions.RemoveEmptyEntries
                                        | StringSplitOptions.TrimEntries
                                );
                            var strictTokens = SplitTokensLocal(strictNorm);
                            var looseTokens = SplitTokensLocal(looseNorm);
                            // Sanitize like core (letters/digits only)
                            string Sanitize(string t)
                            {
                                if (string.IsNullOrEmpty(t))
                                    return string.Empty;
                                var sbLocal = new System.Text.StringBuilder(t.Length);
                                foreach (var ch in t)
                                    if (char.IsLetterOrDigit(ch))
                                        sbLocal.Append(ch);
                                return sbLocal.ToString();
                            }
                            var sanitized = strictTokens
                                .Select(Sanitize)
                                .Where(san => !string.IsNullOrWhiteSpace(san))
                                .ToArray();
                            string reason;
                            if (sf == SearchFields.None)
                                reason = "noFields";
                            else if (strictTokens.Length == 0 && looseTokens.Length == 0)
                                reason = "noTokens";
                            else if (sanitized.Length == 0)
                                reason = "allTokensSanitizedEmpty";
                            else
                                reason = "ftsNoMatch";
                            var qSample = q.Length > 40 ? q.Substring(0, 40) + "…" : q;
                            Log.Debug(
                                "MainSearch",
                                $"cache-diag mode={(andMode ? "AND" : "OR")} reason={reason} fields={fieldFlags} strictTok={strictTokens.Length} looseTok={looseTokens.Length} sanitizedTok={sanitized.Length} cultures={(cacheAttempted ? "?" : "?")} qSample='{qSample}'"
                            );
                        }
                        catch
                        { /* swallow diag errors */
                        }
                        if (!cacheAttempted)
                        {
                            Log.Debug(
                                "MainSearch",
                                $"cache-skipped mode={(andMode ? "AND" : "OR")} qLen={q.Length}"
                            );
                        }
                        else if (!cacheSucceeded)
                        {
                            Log.Debug(
                                "MainSearch",
                                $"cache-unavailable mode={(andMode ? "AND" : "OR")} qLen={q.Length}"
                            );
                        }
                        else
                        {
                            Log.Debug(
                                "MainSearch",
                                $"cache-empty mode={(andMode ? "AND" : "OR")} qLen={q.Length}"
                            );
                        }
                        // If cache missed on a single-token query, honor fallback suppression and do not resurrect
                        // results via in-memory (those would include suppressed fallback cultures like OS / en-US when primary exists).
                        var tokenCount = 0;
                        foreach (
                            var part in q.Split(
                                new[] { ' ', '\t' },
                                StringSplitOptions.RemoveEmptyEntries
                            )
                        )
                        {
                            if (part.Length > 0)
                                tokenCount++;
                            if (tokenCount > 10)
                                break; // hard cap; we only distinguish >1
                        }
                        // Recompute minimal context for fallback policy (these locals are out of original scope here)
                        var stLocal = SettingsService.Instance.LoadSettings();
                        var slotsLocal = PolicyPlusCore.Utilities.CulturePreference.Build(
                            new PolicyPlusCore.Utilities.CulturePreference.BuildOptions(
                                Primary: string.IsNullOrWhiteSpace(stLocal.Language)
                                    ? System.Globalization.CultureInfo.CurrentUICulture.Name
                                    : stLocal.Language!,
                                Second: stLocal.SecondLanguage,
                                SecondEnabled: stLocal.SecondLanguageEnabled ?? false,
                                OsUiCulture: System.Globalization.CultureInfo.CurrentUICulture.Name,
                                EnablePrimaryFallback: stLocal.PrimaryLanguageFallbackEnabled
                                    ?? false
                            )
                        );
                        var skipMem =
                            PolicyPlusCore.Utilities.SearchFallbackPolicy.ShouldSkipMemoryFallback(
                                tokenCount,
                                slotsLocal
                            );
                        if (skipMem)
                        {
                            matches = new();
                            suggestions = BuildSuggestions(q, allowedSet);
                        }
                        else
                        {
                            matches = MatchPolicies(q, baseList, out var allowed2);
                            suggestions = BuildSuggestions(q, allowed2);
                        }
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
                        catch (Exception exLang)
                        {
                            Log.Debug("MainSearch", "cache SearchAsync failed: " + exLang.Message);
                        }
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
                            catch (Exception exSugg)
                            {
                                Log.Debug(
                                    "MainSearch",
                                    "suggestion list open failed: " + exSugg.Message
                                );
                            }
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
                        long totalMs =
                            swTotal?.ElapsedMilliseconds
                            ?? (swCompute.ElapsedMilliseconds + swBind.ElapsedMilliseconds);
                        Log.Debug(
                            "MainSearch",
                            $"SearchPerf qLen={q.Length} computeMs={swCompute.ElapsedMilliseconds} bindMs={swBind.ElapsedMilliseconds} totalMs={totalMs}"
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
                        if (swTotal != null)
                        {
                            swTotal.Stop();
                            Log.Trace(
                                "MainSearch",
                                $"RunAsyncSearchAndBind finish totalMs={swTotal.ElapsedMilliseconds} cache={(IsAdmxCacheEnabled() ? "on" : "off")}"
                            );
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Debug("MainSearch", "Finish spinner hide failed: " + ex.Message);
                    }
                });
            }
        }

        private void RunAsyncFilterAndBind(bool showBaselineOnEmpty = true)
        {
            var swTotal = System.Diagnostics.Stopwatch.StartNew();
            Log.Trace(
                "MainSearch",
                $"RunAsyncFilterAndBind start baseline={showBaselineOnEmpty} cache={(IsAdmxCacheEnabled() ? "on" : "off")}"
            );
            _searchDebounceCts?.Cancel();
            _searchDebounceCts = new System.Threading.CancellationTokenSource();
            var token = _searchDebounceCts.Token;
            try
            {
                PreserveScrollPosition();
            }
            catch (Exception ex)
            {
                Log.Debug("MainSearch", "PreserveScrollPosition failed: " + ex.Message);
            }
            try
            {
                var spinner2 = GetSearchSpinner();
                if (spinner2 != null)
                {
                    spinner2.Visibility = Visibility.Visible;
                    spinner2.IsActive = true;
                }
            }
            catch (Exception ex)
            {
                Log.Debug("MainSearch", "Show spinner (filter) failed: " + ex.Message);
            }
            var applies = _appliesFilter;
            var category = _selectedCategory;
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
                    catch (Exception ex)
                    {
                        Log.Debug("MainSearch", "ShowBaselineSuggestions failed: " + ex.Message);
                    }
                    seq = ApplyBookmarkFilterIfNeeded(seq);
                    seq = ApplyConfiguredFilterIfNeeded(seq, comp, user);
                    items = seq.ToList();
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
                            $"RunAsyncFilterAndBind finish totalMs={swTotal.ElapsedMilliseconds} cache={(IsAdmxCacheEnabled() ? "on" : "off")}"
                        );
                    }
                    catch (Exception ex)
                    {
                        Log.Debug(
                            "MainSearch",
                            "Finish spinner hide (filter) failed: " + ex.Message
                        );
                    }
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
                    $"RunImmediateFilterAndBind start baseline={showBaselineOnEmpty} cache={(IsAdmxCacheEnabled() ? "on" : "off")}"
                );
                var decision = EvaluateDecision();
                var ctx = PolicySourceAccessor.Acquire();
                var seq = BaseSequenceForFilters(decision.IncludeSubcategoryPolicies);
                seq = ApplyBookmarkFilterIfNeeded(seq);
                seq = ApplyConfiguredFilterIfNeeded(seq, ctx.Comp, ctx.User);
                BindSequenceEnhanced(seq, decision, forceComputeStates: _forceComputeStatesOnce);
                UpdateSearchClearButtonVisibility();
                if (showBaselineOnEmpty)
                    // Only open baseline suggestions if the search box is already focused.
                    ShowBaselineSuggestions(onlyIfFocused: true);
                sw.Stop();
                Log.Debug(
                    "MainSearch",
                    $"ImmediateFilterPerf count={_visiblePolicies?.Count} totalMs={sw.ElapsedMilliseconds} cache={(IsAdmxCacheEnabled() ? "on" : "off")}"
                );
            }
            catch (Exception ex)
            {
                Log.Debug("MainSearch", "ImmediateFilterAndBind failed: " + ex.Message);
            }
        }

        private void UpdateEmptyHint(string? q)
        {
            try
            {
                // Resolve panel and early exit if missing.
                var panel = RootGrid?.FindName("EmptyHintPanel") as FrameworkElement;
                if (panel == null)
                    return;
                if (_visiblePolicies != null && _visiblePolicies.Count > 0)
                {
                    panel.Visibility = Visibility.Collapsed;
                    return;
                }

                // Suppress empty hint when only a category filter is active (no search / other filters)
                // and the selected category has no direct policies but at least one matching policy exists in descendant subcategories.
                if (
                    _visiblePolicies != null
                    && _visiblePolicies.Count == 0
                    && _selectedCategory != null
                    && !_configuredOnly
                    && !_bookmarksOnly
                    && string.IsNullOrWhiteSpace(q)
                    && HasAnyPoliciesInSubcategoriesForCurrentApplies(_selectedCategory)
                )
                {
                    panel.Visibility = Visibility.Collapsed;
                    return;
                }

                bool configured = _configuredOnly;
                bool bookmarks = _bookmarksOnly;
                bool categoryFiltered = _selectedCategory != null;
                var btnConf = RootGrid?.FindName("HintDisableConfiguredOnly") as FrameworkElement;
                var btnBmk = RootGrid?.FindName("HintDisableBookmarksOnly") as FrameworkElement;
                var btnCat = RootGrid?.FindName("HintClearCategory") as FrameworkElement;
                var btnAll = RootGrid?.FindName("HintClearAllFilters") as FrameworkElement;
                if (btnConf != null)
                    btnConf.Visibility = configured ? Visibility.Visible : Visibility.Collapsed;
                if (btnBmk != null)
                    btnBmk.Visibility = bookmarks ? Visibility.Visible : Visibility.Collapsed;
                if (btnCat != null)
                    btnCat.Visibility = categoryFiltered
                        ? Visibility.Visible
                        : Visibility.Collapsed;
                if (btnAll != null)
                {
                    int activeCount =
                        (configured ? 1 : 0) + (bookmarks ? 1 : 0) + (categoryFiltered ? 1 : 0);
                    btnAll.Visibility =
                        activeCount >= 2 ? Visibility.Visible : Visibility.Collapsed;
                }

                var msg = RootGrid?.FindName("EmptyHintMessage") as TextBlock;
                var confText = RootGrid?.FindName("HintDisableConfiguredOnlyText") as TextBlock;
                var bmkText = RootGrid?.FindName("HintDisableBookmarksOnlyText") as TextBlock;
                var catText = RootGrid?.FindName("HintClearCategoryText") as TextBlock;
                if (confText != null)
                {
                    confText.Text =
                        _deltaConfigured > 0
                            ? $"Disable 'Configured only' filter ( +{_deltaConfigured} )"
                            : "Disable 'Configured only' filter";
                }
                if (bmkText != null)
                {
                    bmkText.Text =
                        _deltaBookmarks > 0
                            ? $"Disable 'Bookmarks only' filter ( +{_deltaBookmarks} )"
                            : "Disable 'Bookmarks only' filter";
                }
                if (catText != null && categoryFiltered)
                {
                    catText.Text =
                        _deltaCategory > 0
                            ? $"Clear category filter ( +{_deltaCategory} )"
                            : "Clear category filter";
                }
                if (msg != null)
                {
                    // Base fallback messages are overridden below for specific zero-result causes.
                    if (configured || bookmarks || categoryFiltered)
                        msg.Text =
                            "No results due to active filters.\nDisable filters below to broaden the result.";
                    else if (!string.IsNullOrWhiteSpace(q))
                        msg.Text =
                            "No policies match the current search query.\nTry different or fewer keywords.";
                    else
                        msg.Text = "No policies to display.";
                }

                // Override previously computed approximate deltas with precise values when we are in a zero-result state.
                // We only recompute when zero and at least one of the toggle filters is active to avoid cost on normal paths.
                if (_visiblePolicies != null && _visiblePolicies.Count == 0)
                {
                    try
                    {
                        // Recompute counts hypothetically removing each filter independently while keeping the search query and other filters.
                        // This produces the true number of items that would appear if ONLY that filter were disabled.
                        if (configured)
                        {
                            _deltaConfigured = ComputeCountForFilters(
                                configuredOnly: false,
                                bookmarksOnly: bookmarks,
                                category: _selectedCategory,
                                query: q
                            );
                            if (confText != null)
                            {
                                confText.Text =
                                    _deltaConfigured > 0
                                        ? $"Disable 'Configured only' filter ( +{_deltaConfigured} )"
                                        : "Disable 'Configured only' filter";
                            }
                        }
                        if (bookmarks)
                        {
                            _deltaBookmarks = ComputeCountForFilters(
                                configuredOnly: configured,
                                bookmarksOnly: false,
                                category: _selectedCategory,
                                query: q
                            );
                            if (bmkText != null)
                            {
                                bmkText.Text =
                                    _deltaBookmarks > 0
                                        ? $"Disable 'Bookmarks only' filter ( +{_deltaBookmarks} )"
                                        : "Disable 'Bookmarks only' filter";
                            }
                        }
                        if (categoryFiltered)
                        {
                            _deltaCategory = ComputeCountForFilters(
                                configuredOnly: configured,
                                bookmarksOnly: bookmarks,
                                category: null, // clear category
                                query: q
                            );
                            if (catText != null)
                            {
                                catText.Text =
                                    _deltaCategory > 0
                                        ? $"Clear category filter ( +{_deltaCategory} )"
                                        : "Clear category filter";
                            }
                        }

                        // Specialized message overrides.
                        if (msg != null)
                        {
                            // 1. Bookmark-only with zero bookmarks existing at all.
                            if (bookmarks && !_hasAnyBookmarks)
                                msg.Text =
                                    "No bookmarked policies.\nAdd bookmarks or disable the filter.";
                            // 2. Configured-only with no configured policies in scope.
                            else if (configured && !_hasAnyConfiguredInScope)
                                msg.Text =
                                    "No configured policies in the current scope.\nConfigure a policy or disable the filter.";
                            // 3. Category-only (no search, no other filters) and intrinsically empty category.
                            else if (
                                categoryFiltered
                                && !configured
                                && !bookmarks
                                && string.IsNullOrWhiteSpace(q)
                            )
                            {
                                // Treat as intrinsically empty if clearing category introduces any policies.
                                if (_deltaCategory > 0)
                                    msg.Text = "This category contains no policies."; // short enough, single line
                            }
                            // 4. Search yields zero even after clearing all filters (suggest different keywords).
                            else if (!string.IsNullOrWhiteSpace(q))
                            {
                                var searchOnlyCount = ComputeCountForFilters(false, false, null, q);
                                if (searchOnlyCount == 0)
                                    msg.Text =
                                        "No policies match the current search.\nTry different or fewer keywords.";
                            }
                        }
                    }
                    catch
                    { /* best effort */
                    }
                }
                panel.Visibility = Visibility.Visible;
            }
            catch
            {
                // Best effort; ignore UI errors.
            }
        }

        // Returns true if any descendant category contains at least one policy matching the current applies filter.
        private bool HasAnyPoliciesInSubcategoriesForCurrentApplies(PolicyPlusCategory root)
        {
            foreach (var child in root.Children)
            {
                if (SubtreeHasPolicyMatchingApplies(child))
                    return true;
            }
            return false;
        }

        private bool SubtreeHasPolicyMatchingApplies(PolicyPlusCategory cat)
        {
            foreach (var p in cat.Policies)
            {
                if (AppliesFilterMatches(p))
                    return true;
            }
            foreach (var ch in cat.Children)
            {
                if (SubtreeHasPolicyMatchingApplies(ch))
                    return true;
            }
            return false;
        }

        private bool AppliesFilterMatches(PolicyPlusPolicy p)
        {
            return _appliesFilter switch
            {
                AdmxPolicySection.User => p.RawPolicy.Section == AdmxPolicySection.User
                    || p.RawPolicy.Section == AdmxPolicySection.Both,
                AdmxPolicySection.Machine => p.RawPolicy.Section == AdmxPolicySection.Machine
                    || p.RawPolicy.Section == AdmxPolicySection.Both,
                _ => true,
            };
        }

        // Computes how many policies would be visible if the specified flags (configuredOnly/bookmarksOnly) were applied
        // with the current category selection and search query. This intentionally mimics the main pipeline but is simplified
        // and only used for zero-result hint recalculation so minor perf cost is acceptable.
        private int ComputeCountForFilters(
            bool configuredOnly,
            bool bookmarksOnly,
            PolicyPlusCategory? category,
            string? query
        )
        {
            // Build decision with hypothetical flags.
            bool hasCategory = category != null;
            bool hasSearch = !string.IsNullOrWhiteSpace(query);
            var decision = FilterDecisionEngine.Evaluate(
                hasCategory,
                hasSearch,
                configuredOnly,
                bookmarksOnly,
                _limitUnfilteredTo1000
            );
            // Temporarily substitute selected category for base sequence generation.
            var originalCat = _selectedCategory;
            _selectedCategory = category;
            IEnumerable<PolicyPlusPolicy> seq = BaseSequenceForFilters(
                decision.IncludeSubcategoryPolicies
            );
            _selectedCategory = originalCat; // restore
            if (bookmarksOnly)
            {
                var ids = BookmarkService.Instance.ActiveIds;
                if (ids == null || ids.Count == 0)
                    return 0;
                var set = new HashSet<string>(ids, StringComparer.OrdinalIgnoreCase);
                seq = seq.Where(p => set.Contains(p.UniqueID));
            }
            if (configuredOnly)
            {
                var ctx = PolicySourceAccessor.Acquire();
                var pending =
                    PendingChangesService.Instance.Pending?.ToList() ?? new List<PendingChange>();
                var list = seq as IList<PolicyPlusPolicy> ?? seq.ToList();
                if (ctx.Comp == null && ctx.User == null && pending.Count == 0)
                    return 0;
                seq = list.Where(p =>
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
                                        ctx.User != null
                                        && PolicyProcessing.GetPolicyState(ctx.User, p)
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
                                && pc2.Scope.Equals("Computer", StringComparison.OrdinalIgnoreCase)
                            );
                            effComp =
                                pc != null
                                    ? (
                                        pc.DesiredState
                                        is PolicyState.Enabled
                                            or PolicyState.Disabled
                                    )
                                    : (
                                        ctx.Comp != null
                                        && PolicyProcessing.GetPolicyState(ctx.Comp, p)
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
            // Apply search last.
            List<PolicyPlusPolicy> listAfter;
            if (!string.IsNullOrWhiteSpace(query))
            {
                listAfter = MatchPolicies(query, seq, out _);
            }
            else
            {
                listAfter = seq as List<PolicyPlusPolicy> ?? seq.ToList();
                if (decision.Limit.HasValue && listAfter.Count > decision.Limit.Value)
                    listAfter = listAfter.Take(decision.Limit.Value).ToList();
            }
            return listAfter.Count;
        }
    }
}
