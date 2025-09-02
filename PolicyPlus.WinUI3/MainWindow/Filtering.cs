using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Windows.System;
using PolicyPlus;
using PolicyPlus.WinUI3.Models;
using PolicyPlus.WinUI3.Services;
using PolicyPlus.WinUI3.Dialogs; // FindByRegistryWinUI
using System;
using System.Collections.Generic;
using System.Linq;

namespace PolicyPlus.WinUI3
{
    public sealed partial class MainWindow
    {
        private IEnumerable<PolicyPlusPolicy> BaseSequenceForFilters(bool includeSubcategories)
        {
            IEnumerable<PolicyPlusPolicy> seq = _allPolicies;
            if (_appliesFilter == AdmxPolicySection.Machine)
                seq = seq.Where(p => p.RawPolicy.Section == AdmxPolicySection.Machine || p.RawPolicy.Section == AdmxPolicySection.Both);
            else if (_appliesFilter == AdmxPolicySection.User)
                seq = seq.Where(p => p.RawPolicy.Section == AdmxPolicySection.User || p.RawPolicy.Section == AdmxPolicySection.Both);

            if (_selectedCategory is not null)
            {
                if (includeSubcategories)
                {
                    var allowed = new HashSet<string>();
                    CollectPoliciesRecursive(_selectedCategory, allowed);
                    seq = seq.Where(p => allowed.Contains(p.UniqueID));
                }
                else
                {
                    var direct = new HashSet<string>(_selectedCategory.Policies.Select(p => p.UniqueID));
                    seq = seq.Where(p => direct.Contains(p.UniqueID));
                }
            }

            if (_configuredOnly)
            {
                EnsureLocalSources();
                var pending = PendingChangesService.Instance.Pending?.ToList() ?? new List<PendingChange>();
                if (_compSource != null || _userSource != null || pending.Count > 0)
                {
                    seq = seq.Where(p =>
                    {
                        bool configured = false;
                        try
                        {
                            bool effUser = false, effComp = false;

                            if (p.RawPolicy.Section == AdmxPolicySection.User || p.RawPolicy.Section == AdmxPolicySection.Both)
                            {
                                var pendUser = pending.FirstOrDefault(pc => string.Equals(pc.PolicyId, p.UniqueID, StringComparison.OrdinalIgnoreCase)
                                                                         && string.Equals(pc.Scope, "User", StringComparison.OrdinalIgnoreCase));
                                if (pendUser != null)
                                {
                                    effUser = (pendUser.DesiredState == PolicyState.Enabled || pendUser.DesiredState == PolicyState.Disabled);
                                }
                                else if (_userSource != null)
                                {
                                    var st = PolicyProcessing.GetPolicyState(_userSource, p);
                                    effUser = (st == PolicyState.Enabled || st == PolicyState.Disabled);
                                }
                            }

                            if (p.RawPolicy.Section == AdmxPolicySection.Machine || p.RawPolicy.Section == AdmxPolicySection.Both)
                            {
                                var pendComp = pending.FirstOrDefault(pc => string.Equals(pc.PolicyId, p.UniqueID, StringComparison.OrdinalIgnoreCase)
                                                                         && string.Equals(pc.Scope, "Computer", StringComparison.OrdinalIgnoreCase));
                                if (pendComp != null)
                                {
                                    effComp = (pendComp.DesiredState == PolicyState.Enabled || pendComp.DesiredState == PolicyState.Disabled);
                                }
                                else if (_compSource != null)
                                {
                                    var st = PolicyProcessing.GetPolicyState(_compSource, p);
                                    effComp = (st == PolicyState.Enabled || st == PolicyState.Disabled);
                                }
                            }

                            configured = effUser || effComp;
                        }
                        catch { }
                        return configured;
                    });
                }
            }
            return seq;
        }

        private void ApplyFiltersAndBind(string query = "", PolicyPlusCategory? category = null)
        {
            if (PolicyList == null || PolicyCount == null) return;
            if (category != null) _selectedCategory = category;

            PreserveScrollPosition();
            UpdateSearchPlaceholder();

            bool searching = !string.IsNullOrWhiteSpace(query);
            bool flat = searching || _configuredOnly;
            IEnumerable<PolicyPlusPolicy> seq = BaseSequenceForFilters(includeSubcategories: flat);
            if (searching)
            {
                var qLower = query.ToLowerInvariant();
                var allowed = new HashSet<string>(seq.Select(p => p.UniqueID), StringComparer.OrdinalIgnoreCase);
                var matched = new List<PolicyPlusPolicy>();
                bool smallSubset = allowed.Count > 0 && allowed.Count < (_allPolicies.Count / 2);

                if (smallSubset)
                {
                    foreach (var id in allowed)
                    {
                        if (!_searchIndexById.TryGetValue(id, out var e)) continue;
                        if (PolicyMatchesQuery(e, query, qLower))
                            matched.Add(e.Policy);
                    }
                }
                else
                {
                    foreach (var e in _searchIndex)
                    {
                        if (!allowed.Contains(e.Policy.UniqueID)) continue;
                        if (PolicyMatchesQuery(e, query, qLower))
                            matched.Add(e.Policy);
                    }
                }
                seq = matched;
            }

            BindSequenceEnhanced(seq, flat);
            RestorePositionOrSelection();
            // Ensure a baseline state exists so first change enables Back
            if (ViewNavigationService.Instance.Current == null)
            {
                MaybePushCurrentState();
            }
        }

        private bool PolicyMatchesQuery((PolicyPlusPolicy Policy, string NameLower, string IdLower, string DescLower) e, string query, string qLower)
        {
            bool hit = false;

            // Fast text checks first
            if (_searchInName && e.NameLower.Contains(qLower))
                hit = true;
            if (!hit && _searchInId && e.IdLower.Contains(qLower))
                hit = true;
            if (!hit && _searchInDescription && e.DescLower.Contains(qLower))
                hit = true;

            // Comments (cheap) before registry scans
            if (!hit && _searchInComments)
            {
                if (_compComments.TryGetValue(e.Policy.UniqueID, out var c1) && !string.IsNullOrEmpty(c1) && c1.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                    hit = true;
                else if (_userComments.TryGetValue(e.Policy.UniqueID, out var c2) && !string.IsNullOrEmpty(c2) && c2.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                    hit = true;
            }

            // Registry scans last (expensive)
            if (!hit && (_searchInRegistryKey || _searchInRegistryValue))
            {
                bool keyHit = false, valHit = false;
                if (_searchInRegistryKey)
                    keyHit = FindByRegistryWinUI.SearchRegistry(e.Policy, qLower, string.Empty, allowSubstring: true);
                if (!keyHit && _searchInRegistryValue)
                    valHit = FindByRegistryWinUI.SearchRegistryValueNameOnly(e.Policy, qLower, allowSubstring: true);
                if (keyHit || valHit) hit = true;
            }

            return hit;
        }

        private void BindSequenceEnhanced(IEnumerable<PolicyPlusPolicy> seq, bool flat)
        {
            EnsureLocalSources();

            if (flat)
            {
                var ordered = seq.OrderBy(p => p.DisplayName, StringComparer.InvariantCultureIgnoreCase)
                                 .ThenBy(p => p.UniqueID, StringComparer.InvariantCultureIgnoreCase)
                                 .ToList();
                _visiblePolicies = ordered.ToList();

                var rows = ordered.Select(p => (object)PolicyListRow.FromPolicy(p, _compSource, _userSource)).ToList();
                PolicyList.ItemsSource = rows;

                PolicyCount.Text = $"{_visiblePolicies.Count} / {_allPolicies.Count} policies";
                TryRestoreSelectionAsync(rows);
                MaybePushCurrentState();
                return;
            }

            var grouped = seq.GroupBy(p => p.DisplayName, System.StringComparer.InvariantCultureIgnoreCase);
            _nameGroups = grouped.ToDictionary(g => g.Key, g => g.ToList(), StringComparer.InvariantCultureIgnoreCase);
            var representatives = grouped.Select(g => PickRepresentative(g)).OrderBy(p => p.DisplayName).ToList();

            _visiblePolicies = representatives;

            var groupRows = representatives.Select(p =>
            {
                _nameGroups.TryGetValue(p.DisplayName, out var groupList);
                groupList ??= new List<PolicyPlusPolicy> { p };
                return (object)PolicyListRow.FromGroup(p, groupList, _compSource, _userSource);
            }).ToList<object>();

            if (_selectedCategory != null && !_configuredOnly)
            {
                var items = new List<object>();
                var children = _selectedCategory.Children
                    .Where(c => !_hideEmptyCategories || HasAnyVisiblePolicyInCategory(c))
                    .OrderBy(c => c.DisplayName)
                    .Select(c => (object)PolicyListRow.FromCategory(c))
                    .ToList();
                items.AddRange(children);
                items.AddRange(groupRows);
                PolicyList.ItemsSource = items;
                TryRestoreSelectionAsync(items);
            }
            else
            {
                PolicyList.ItemsSource = groupRows;
                TryRestoreSelectionAsync(groupRows);
            }

            PolicyCount.Text = $"{_visiblePolicies.Count} / {_totalGroupCount} policies";
            MaybePushCurrentState();
        }

        private PolicyPlusPolicy PickRepresentative(IGrouping<string, PolicyPlusPolicy> g)
        {
            var list = g.ToList();
            var both = list.FirstOrDefault(p => p.RawPolicy.Section == AdmxPolicySection.Both);
            if (both != null) return both;
            var comp = list.FirstOrDefault(p => p.RawPolicy.Section == AdmxPolicySection.Machine);
            return comp ?? list[0];
        }

        private void CollectPoliciesRecursive(PolicyPlusCategory cat, HashSet<string> sink)
        {
            foreach (var p in cat.Policies) sink.Add(p.UniqueID);
            foreach (var child in cat.Children) CollectPoliciesRecursive(child, sink);
        }

        private void PolicyList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var row = PolicyList?.SelectedItem;
            var p = (row as PolicyListRow)?.Policy ?? row as PolicyPlusPolicy;
            SetDetails(p);
        }

        private void SetDetails(PolicyPlusPolicy? p)
        {
            if (DetailTitle == null) return;
            if (p is null)
            {
                DetailTitle.Blocks.Clear();
                DetailId.Blocks.Clear();
                DetailCategory.Blocks.Clear();
                DetailApplies.Blocks.Clear();
                DetailSupported.Blocks.Clear();
                if (DetailExplain != null) DetailExplain.Blocks.Clear();
                return;
            }
            SetPlainText(DetailTitle, p.DisplayName);
            SetPlainText(DetailId, p.UniqueID);
            SetPlainText(DetailCategory, p.Category is null ? string.Empty : $"Category: {p.Category.DisplayName}");
            var applies = p.RawPolicy.Section switch { AdmxPolicySection.Machine => "Computer", AdmxPolicySection.User => "User", _ => "Both" };
            SetPlainText(DetailApplies, $"Applies to: {applies}");
            SetPlainText(DetailSupported, p.SupportedOn is null ? string.Empty : $"Supported on: {p.SupportedOn.DisplayName}");
            SetExplanationText(p.DisplayExplanation ?? string.Empty);
        }

        private static void SetPlainText(RichTextBlock rtb, string text)
        {
            rtb.Blocks.Clear();
            var p = new Paragraph();
            p.Inlines.Add(new Run { Text = text ?? string.Empty });
            rtb.Blocks.Add(p);
        }

        private static bool IsInsideDoubleQuotes(string s, int index)
        {
            bool inQuote = false;
            int i = 0;
            while (i < index)
            {
                if (s[i] == '"')
                {
                    int bs = 0; int j = i - 1; while (j >= 0 && s[j] == '\\') { bs++; j--; }
                    if ((bs % 2) == 0) inQuote = !inQuote;
                }
                i++;
            }
            return inQuote;
        }

        private void SetExplanationText(string text)
        {
            if (DetailExplain == null) return;
            DetailExplain.Blocks.Clear();
            var para = new Paragraph();
            if (string.IsNullOrEmpty(text)) { DetailExplain.Blocks.Add(para); return; }

            int lastIndex = 0;
            foreach (Match m in UrlRegex.Matches(text))
            {
                if (IsInsideDoubleQuotes(text, m.Index))
                    continue;

                if (m.Index > lastIndex)
                {
                    var before = text.Substring(lastIndex, m.Index - lastIndex);
                    para.Inlines.Add(new Run { Text = before });
                }

                string url = m.Value;
                var link = new Hyperlink();
                link.Inlines.Add(new Run { Text = url });
                link.Click += async (s, e) =>
                {
                    if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                    {
                        try { await Launcher.LaunchUriAsync(uri); } catch { }
                    }
                };
                para.Inlines.Add(link);
                lastIndex = m.Index + m.Length;
            }
            if (lastIndex < text.Length)
            {
                para.Inlines.Add(new Run { Text = text.Substring(lastIndex) });
            }
            DetailExplain.Blocks.Add(para);
        }
    }
}
