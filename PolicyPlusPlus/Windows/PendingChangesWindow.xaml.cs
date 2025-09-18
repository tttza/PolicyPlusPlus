using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using PolicyPlusCore.Core;
using PolicyPlusCore.IO;
using PolicyPlusPlus.Services;
using PolicyPlusPlus.Utils;
using PolicyPlusPlus.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json; // added for JsonElement handling
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace PolicyPlusPlus.Windows
{
    public sealed partial class PendingChangesWindow : Window
    {
        public static event EventHandler? ChangesAppliedOrDiscarded;

        private List<PendingChange> _pendingView = new();
        private List<HistoryRecord> _historyView = new();
        private IElevationService _elevation;

        public PendingChangesWindow() : this(new ElevationServiceAdapter()) { }

        public PendingChangesWindow(IElevationService elevation)
        {
            _elevation = elevation;
            InitializeComponent();
            Title = "Pending changes";
            ChildWindowCommon.Initialize(this, 900, 640, ApplyThemeResources);
            BtnClose.Click += (s, e) => this.Close();
            BtnApplySelected.Click += BtnApplySelected_Click;
            BtnDiscardSelected.Click += BtnDiscardSelected_Click;
            BtnClearFilters.Click += (s, e) => { if (SearchBox != null) SearchBox.Text = string.Empty; if (ScopeFilter != null) ScopeFilter.SelectedIndex = 0; if (OperationFilter != null) OperationFilter.SelectedIndex = 0; if (HistoryTimeRange != null) HistoryTimeRange.SelectedIndex = 0; if (HistoryType != null) HistoryType.SelectedIndex = 0; if (HistorySearch != null) HistorySearch.Text = string.Empty; RefreshViews(); };
            if (RootShell != null)
                RootShell.Loaded += (s, e) => { RefreshViews(); PendingChangesWindow_Loaded(s, e); };
            PendingList.DoubleTapped += (s, e) => Pending_ContextView_Click(s, e);
            var main = App.Window as MainWindow;
            if (main != null) { main.Saved += (s, e) => { ShowLocalInfo("Saved."); RefreshViews(); }; }
            try { SubscribeCollectionChanges(); } catch { }
            try { EventHub.PendingAppliedOrDiscarded += OnPendingAppliedOrDiscarded; } catch { }
            try { EventHub.PendingQueueChanged += OnPendingQueueChanged; } catch { }
            try { EventHub.HistoryChanged += OnHistoryChanged; } catch { }
            Closed += (s, e) =>
            {
                try { EventHub.PendingAppliedOrDiscarded -= OnPendingAppliedOrDiscarded; } catch { }
                try { EventHub.PendingQueueChanged -= OnPendingQueueChanged; } catch { }
                try { EventHub.HistoryChanged -= OnHistoryChanged; } catch { }
            };
        }

        private void OnPendingAppliedOrDiscarded(IReadOnlyCollection<string> ids) { try { RefreshViews(); } catch { } }
        private void OnPendingQueueChanged(IReadOnlyCollection<string> add, IReadOnlyCollection<string> rem) { try { RefreshViews(); } catch { } }
        private void OnHistoryChanged() { try { RefreshViews(); } catch { } }

        public void SelectHistoryTab()
        {
            try { if (MainTabs != null && HistoryTab != null) MainTabs.SelectedItem = HistoryTab; } catch { }
        }

        private void Accel_SaveAll(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        { BtnSaveAll_Click(this, new RoutedEventArgs()); args.Handled = true; }
        private void Accel_SaveSelected(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        { BtnApplySelected_Click(this, new RoutedEventArgs()); args.Handled = true; }
        private void Accel_DiscardSelected(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        { BtnDiscardSelected_Click(this, new RoutedEventArgs()); args.Handled = true; }
        private void Accel_DiscardAll(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        { BtnDiscardAll_Click(this, new RoutedEventArgs()); args.Handled = true; }
        private void Accel_FocusSearch(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        { try { if ((MainTabs.SelectedItem as TabViewItem) == PendingTab) SearchBox?.Focus(FocusState.Programmatic); else HistorySearch?.Focus(FocusState.Programmatic); } catch { } args.Handled = true; }
        private void Accel_TabPending(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        { try { MainTabs.SelectedItem = PendingTab; } catch { } args.Handled = true; }
        private void Accel_TabHistory(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        { try { MainTabs.SelectedItem = HistoryTab; } catch { } args.Handled = true; }
        private void Accel_Close(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        { try { this.Close(); } catch { } args.Handled = true; }

        private void MainTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateTabButtonsVisibility();
        }

        private void UpdateTabButtonsVisibility()
        {
            bool pendingActive = (MainTabs.SelectedItem as TabViewItem) == PendingTab;
            BtnApplySelected.Visibility = pendingActive ? Visibility.Visible : Visibility.Collapsed;
            BtnDiscardSelected.Visibility = pendingActive ? Visibility.Visible : Visibility.Collapsed;
            BtnApplyAll.Visibility = pendingActive ? Visibility.Visible : Visibility.Collapsed;
            BtnDiscardAll.Visibility = pendingActive ? Visibility.Visible : Visibility.Collapsed;
            BtnReapplySelected.Visibility = pendingActive ? Visibility.Collapsed : Visibility.Visible;
        }

        private void SubscribeCollectionChanges()
        {
            try
            {
                PendingChangesService.Instance.Pending.CollectionChanged += OnCollectionsChanged;
                PendingChangesService.Instance.History.CollectionChanged += OnCollectionsChanged;
            }
            catch { }
        }
        private void UnsubscribeCollectionChanges()
        {
            try
            {
                PendingChangesService.Instance.Pending.CollectionChanged -= OnCollectionsChanged;
                PendingChangesService.Instance.History.CollectionChanged -= OnCollectionsChanged;
            }
            catch { }
        }
        private void OnCollectionsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                RefreshViews();
                // Persist history to disk when it changes
                try { SettingsService.Instance.SaveHistory(PendingChangesService.Instance.History.ToList()); } catch { }
            });
        }

        // Call once on loaded too
        private void PendingChangesWindow_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateTabButtonsVisibility();
        }

        private async void ShowLocalInfo(string message)
        {
            if (LocalStatusBar == null) return;
            LocalStatusBar.Message = message;
            LocalStatusBar.Severity = InfoBarSeverity.Success;
            LocalStatusBar.IsOpen = true;
            await System.Threading.Tasks.Task.Delay(2500);
            LocalStatusBar.IsOpen = false;
        }

        private void RefreshViews()
        {
            var srcPending = PendingChangesService.Instance.Pending?.ToList() ?? new List<PendingChange>();
            _pendingView = PendingChangesFilter.FilterPending(srcPending, SearchBox?.Text ?? string.Empty,
                ((ScopeFilter?.SelectedItem as ComboBoxItem)?.Content?.ToString()) ?? "Both",
                ((OperationFilter?.SelectedItem as ComboBoxItem)?.Content?.ToString()) ?? "All");
            if (PendingList != null) PendingList.ItemsSource = _pendingView;

            var srcHistory = PendingChangesService.Instance.History?.ToList() ?? new List<HistoryRecord>();
            _historyView = PendingChangesFilter.FilterHistory(srcHistory, HistorySearch?.Text ?? string.Empty,
                ((HistoryType?.SelectedItem as ComboBoxItem)?.Content?.ToString()) ?? "All",
                ((HistoryTimeRange?.SelectedItem as ComboBoxItem)?.Content?.ToString()) ?? "All");
            if (HistoryList != null) HistoryList.ItemsSource = _historyView;

            UpdateSummary();
        }

        private void UpdateSummary()
        {
            if (SummaryText != null)
            {
                var count = PendingChangesService.Instance.Pending.Count;
                SummaryText.Text = PendingChangesFilter.BuildSummary(count);
            }
        }

        private async void BtnSaveAll_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await ApplySelectedAsync(PendingChangesService.Instance.Pending.ToArray());
            }
            catch (Exception ex)
            {
                ShowLocalInfo("Save failed: " + ex.Message);
            }
        }

        private async void BtnApplySelected_Click(object sender, RoutedEventArgs e)
        {
            var items = PendingList.SelectedItems;
            if (items == null || items.Count == 0) return;
            var arr = new PendingChange[items.Count];
            for (int i = 0; i < items.Count; i++) arr[i] = (PendingChange)items[i]!;
            try
            {
                await ApplySelectedAsync(arr);
            }
            catch (Exception ex)
            {
                ShowLocalInfo("Save failed: " + ex.Message);
            }
        }

        private void BtnDiscardSelected_Click(object sender, RoutedEventArgs e)
        {
            var items = PendingList.SelectedItems;
            if (items == null || items.Count == 0) return;
            var arr = new PendingChange[items.Count];
            for (int i = 0; i < items.Count; i++) arr[i] = (PendingChange)items[i]!;
            PendingChangesService.Instance.Discard(arr);
            RefreshViews();
            ChangesAppliedOrDiscarded?.Invoke(this, EventArgs.Empty);
            NotifyDiscarded(arr.Length);
        }

        private void BtnDiscardAll_Click(object sender, RoutedEventArgs e)
        {
            int count = PendingChangesService.Instance.Pending.Count;
            PendingChangesService.Instance.DiscardAll();
            RefreshViews();
            ChangesAppliedOrDiscarded?.Invoke(this, EventArgs.Empty);
            NotifyDiscarded(count);
        }

        private async void Pending_ContextApply_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is PendingChange c)
            {
                try
                {
                    await ApplySelectedAsync(new[] { c });
                }
                catch (Exception ex)
                {
                    ShowLocalInfo("Save failed: " + ex.Message);
                }
            }
        }

        private void Pending_ContextDiscard_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is PendingChange c)
            {
                PendingChangesService.Instance.Discard(c);
                RefreshViews();
                ChangesAppliedOrDiscarded?.Invoke(this, EventArgs.Empty);
            }
        }

        private void Pending_ContextView_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not PendingChange c)
            {
                // fallback to selected item if context is missing
                c = (PendingList?.SelectedItem as PendingChange)!;
                if (c == null) return;
            }

            if (App.Window is not MainWindow main) return;
            var bundle = main.Bundle;
            var compSrc = main.CompSource;
            var userSrc = main.UserSource;
            if (bundle == null || !bundle.Policies.TryGetValue(c.PolicyId, out var pol)) return;
            var section = string.Equals(c.Scope, "User", StringComparison.OrdinalIgnoreCase) ? AdmxPolicySection.User : AdmxPolicySection.Machine;
            var dlg = new DetailPolicyFormattedWindow();
            dlg.Initialize(pol, bundle, compSrc ?? new PolicyLoader(PolicyLoaderSource.LocalGpo, string.Empty, false).OpenSource(), userSrc ?? new PolicyLoader(PolicyLoaderSource.LocalGpo, string.Empty, true).OpenSource(), section);
            dlg.Activate();
        }

        private void Pending_ContextCopyRegPath_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is PendingChange c)
            {
                var dp = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
                dp.SetText(c.Details ?? string.Empty);
                Clipboard.SetContent(dp);
            }
        }

        private void History_ContextCopyName_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is HistoryRecord h)
            {
                var dp = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
                dp.SetText(h.PolicyName ?? string.Empty);
                Clipboard.SetContent(dp);
            }
        }

        private void History_ContextCopyRegPath_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is HistoryRecord h)
            {
                var dp = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
                dp.SetText(h.Details ?? string.Empty);
                Clipboard.SetContent(dp);
            }
        }

        private void SearchBox_TextChanged(object sender, AutoSuggestBoxTextChangedEventArgs e)
        {
            if (e.Reason == AutoSuggestionBoxTextChangeReason.UserInput) RefreshViews();
        }

        private void ScopeFilter_SelectionChanged(object sender, SelectionChangedEventArgs e) => RefreshViews();
        private void OperationFilter_SelectionChanged(object sender, SelectionChangedEventArgs e) => RefreshViews();
        private void HistoryFilter_Changed(object sender, SelectionChangedEventArgs e) => RefreshViews();
        private void HistorySearch_TextChanged(object sender, AutoSuggestBoxTextChangedEventArgs e)
        {
            if (e.Reason == AutoSuggestionBoxTextChangeReason.UserInput) RefreshViews();
        }

        private void ApplyThemeResources()
        {
            try { if (Content is FrameworkElement fe) fe.RequestedTheme = App.CurrentTheme; } catch { }
        }

        private void SetSaving(bool saving)
        {
            try { if (SavingOverlay != null) SavingOverlay.Visibility = saving ? Visibility.Visible : Visibility.Collapsed; } catch { }
        }

        private async Task ApplySelectedAsync(IEnumerable<PendingChange> items)
        {
            if (items == null) return;
            SetSaving(true);
            try
            {
                if (App.Window is not MainWindow main) return;
                var bundle = main.Bundle;
                if (bundle == null) { return; }
                var appliedList = items.ToList();
                var mgr = PolicySourceManager.Instance;
                var (ok, error) = await mgr.ApplyPendingAsync(bundle, appliedList.ToArray(), new ElevationServiceAdapter());
                if (ok)
                {
                    PendingChangesService.Instance.Applied(appliedList.ToArray());
                    try { SettingsService.Instance.SaveHistory(PendingChangesService.Instance.History.ToList()); } catch { }
                    try { mgr.Refresh(); } catch { }
                    RefreshViews();
                    ChangesAppliedOrDiscarded?.Invoke(this, EventArgs.Empty);
                    NotifyApplied(appliedList.Count);
                    try
                    {
                        var affected = appliedList.Select(p => p.PolicyId).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                        EventHub.PublishPolicySourcesRefreshed(affected);
                        EventHub.PublishPendingAppliedOrDiscarded(affected);
                    }
                    catch { }
                }
                else if (!string.IsNullOrEmpty(error)) ShowLocalInfo("Save failed: " + error);
            }
            finally { SetSaving(false); }
        }

        private void NotifyApplied(int count)
        {
            var msg = count == 1 ? "1 change saved." : $"{count} changes saved.";
            ShowLocalInfo(msg);
            if (App.Window is MainWindow mw)
            {
                try { mw.GetType().GetMethod("ShowInfo", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.Invoke(mw, new object[] { msg, InfoBarSeverity.Success }); } catch { }
            }
        }

        private void NotifyDiscarded(int count)
        {
            var msg = count == 1 ? "1 change discarded." : $"{count} changes discarded.";
            ShowLocalInfo(msg);
            if (App.Window is MainWindow mw)
            {
                try { mw.GetType().GetMethod("ShowInfo", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.Invoke(mw, new object[] { msg, InfoBarSeverity.Informational }); } catch { }
            }
        }

        private async Task ExecuteReapplyAsync(HistoryRecord h)
        {
            if (h == null || string.IsNullOrEmpty(h.PolicyId)) return;
            if (App.Window is not MainWindow main) return;
            var bundle = main.Bundle;
            if (bundle == null || !bundle.Policies.TryGetValue(h.PolicyId, out var pol)) return;
            var normalizedOptions = NormalizeOptions(h.Options);
            var pending = new PendingChange
            {
                PolicyId = h.PolicyId,
                PolicyName = h.PolicyName,
                Scope = h.Scope,
                DesiredState = h.DesiredState,
                Action = h.DesiredState switch { PolicyState.Enabled => "Enable", PolicyState.Disabled => "Disable", _ => "Clear" },
                Options = normalizedOptions,
                Details = h.Details,
                DetailsFull = h.DetailsFull
            };
            SetSaving(true);
            try
            {
                var mgr = PolicySourceManager.Instance;
                var (ok, error) = await mgr.ApplyPendingAsync(bundle, new[] { pending }, new ElevationServiceAdapter());
                if (ok)
                {
                    PendingChangesService.Instance.History.Add(new HistoryRecord
                    {
                        PolicyId = h.PolicyId,
                        PolicyName = h.PolicyName,
                        Scope = h.Scope,
                        Action = pending.Action,
                        Result = "Reapplied",
                        Details = h.Details,
                        DetailsFull = h.DetailsFull,
                        AppliedAt = DateTime.Now,
                        DesiredState = h.DesiredState,
                        Options = normalizedOptions
                    });
                    try { SettingsService.Instance.SaveHistory(PendingChangesService.Instance.History.ToList()); } catch { }
                    try { mgr.Refresh(); } catch { }
                    RefreshViews();
                    ChangesAppliedOrDiscarded?.Invoke(this, EventArgs.Empty);
                    ShowLocalInfo("Reapplied.");
                }
                else ShowLocalInfo(string.IsNullOrEmpty(error) ? "Reapply failed." : "Reapply failed: " + error);
            }
            finally { SetSaving(false); }
        }

        // Legacy sync wrapper kept for minimal impact (unused internally now)
        private void ExecuteReapply(HistoryRecord h) => _ = ExecuteReapplyAsync(h);

        private void History_ContextReapply_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is HistoryRecord h)
            {
                ExecuteReapply(h);
            }
        }

        private void BtnReapplySelected_Click(object sender, RoutedEventArgs e)
        {
            var items = HistoryList?.SelectedItems;
            if (items == null || items.Count == 0) return;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] is HistoryRecord h) ExecuteReapply(h);
            }
        }

        private async void PendingList_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            try
            {
                var selected = PendingList?.SelectedItem as PendingChange;
                if (selected == null) return;
                var section = string.Equals(selected.Scope, "User", StringComparison.OrdinalIgnoreCase) ? AdmxPolicySection.User : AdmxPolicySection.Machine;
                if (App.Window is MainWindow main)
                {
                    await main.OpenEditDialogForPolicyIdAsync(selected.PolicyId, section, true);
                }
            }
            catch { }
        }

        private async void HistoryList_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            try
            {
                var selected = HistoryList?.SelectedItem as HistoryRecord;
                if (selected == null) return;
                var section = string.Equals(selected.Scope, "User", StringComparison.OrdinalIgnoreCase) ? AdmxPolicySection.User : AdmxPolicySection.Machine;
                if (App.Window is MainWindow main)
                {
                    await main.OpenEditDialogForPolicyIdAsync(selected.PolicyId, section, true);
                }
            }
            catch { }
        }

        private static Dictionary<string, object>? NormalizeOptions(Dictionary<string, object>? raw)
        {
            if (raw == null) return null;
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in raw)
            {
                object v = kv.Value ?? string.Empty;
                if (v is JsonElement je)
                {
                    v = ConvertJsonElement(je) ?? string.Empty;
                }
                else if (v is IEnumerable<object> en && v is not string)
                {
                    // Already materialized collection but elements might still be JsonElement
                    var list = new List<object>();
                    foreach (var item in en)
                    {
                        if (item is JsonElement je2) list.Add(ConvertJsonElement(je2) ?? string.Empty);
                        else list.Add(item ?? string.Empty);
                    }
                    v = PostProcessCollection(list);
                }
                result[kv.Key] = v;
            }
            return result;
        }

        private static object PostProcessCollection(List<object> items)
        {
            if (items.Count == 0) return Array.Empty<string>();
            if (items.All(i => i is string)) return items.Cast<string>().ToArray();
            if (items.All(i => i is KeyValuePair<string, string>)) return items.Cast<KeyValuePair<string, string>>().ToList();
            return items.Select(i => Convert.ToString(i) ?? string.Empty).ToArray();
        }

        private static object? ConvertJsonElement(JsonElement je)
        {
            switch (je.ValueKind)
            {
                case JsonValueKind.String: return je.GetString();
                case JsonValueKind.Number:
                    if (je.TryGetInt64(out var l))
                    {
                        // Prefer int where possible (enum index expectation)
                        if (l <= int.MaxValue && l >= int.MinValue) return (int)l;
                        return l;
                    }
                    if (je.TryGetDouble(out var d)) return d;
                    return je.ToString();
                case JsonValueKind.True: return true;
                case JsonValueKind.False: return false;
                case JsonValueKind.Array:
                    {
                        var items = new List<object>();
                        foreach (var elem in je.EnumerateArray())
                        {
                            if (elem.ValueKind == JsonValueKind.Object && elem.TryGetProperty("Key", out var keyProp) && elem.TryGetProperty("Value", out var valProp))
                            {
                                items.Add(new KeyValuePair<string, string>(keyProp.GetString() ?? string.Empty, valProp.GetString() ?? string.Empty));
                            }
                            else
                            {
                                items.Add(ConvertJsonElement(elem) ?? string.Empty);
                            }
                        }
                        return PostProcessCollection(items);
                    }
                case JsonValueKind.Object:
                    {
                        // Attempt to materialize as dictionary (named list case)
                        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        bool allSimple = true;
                        foreach (var prop in je.EnumerateObject())
                        {
                            switch (prop.Value.ValueKind)
                            {
                                case JsonValueKind.String:
                                    dict[prop.Name] = prop.Value.GetString() ?? string.Empty; break;
                                case JsonValueKind.Number:
                                    if (prop.Value.TryGetInt64(out var ln)) dict[prop.Name] = ln.ToString(); else if (prop.Value.TryGetDouble(out var dn)) dict[prop.Name] = dn.ToString(); else dict[prop.Name] = prop.Value.ToString();
                                    break;
                                case JsonValueKind.True: dict[prop.Name] = "true"; break;
                                case JsonValueKind.False: dict[prop.Name] = "false"; break;
                                default:
                                    allSimple = false; break;
                            }
                            if (!allSimple) break;
                        }
                        if (allSimple) return dict;
                        return je.ToString();
                    }
                default:
                    return null;
            }
        }
    }
}
