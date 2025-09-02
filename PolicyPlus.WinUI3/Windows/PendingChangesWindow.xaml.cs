using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using PolicyPlus.WinUI3.Utils;
using PolicyPlus.WinUI3.Services;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Pickers;
using WinRT.Interop;
using System.Collections.Specialized;
using System.IO;
using System.Threading.Tasks;
using PolicyPlus; // Core
using PolicyPlus.WinUI3.ViewModels;

namespace PolicyPlus.WinUI3.Windows
{
    public sealed partial class PendingChangesWindow : Window
    {
        public static event EventHandler? ChangesAppliedOrDiscarded;

        private List<PendingChange> _pendingView = new();
        private List<HistoryRecord> _historyView = new();
        private IElevationService _elevation;

        // For app use
        public PendingChangesWindow() : this(new ElevationServiceAdapter()) { }

        // For tests (dependency injection)
        public PendingChangesWindow(IElevationService elevation)
        {
            _elevation = elevation;
            InitializeComponent();
            Title = "Pending changes";

            ApplyThemeResources();
            App.ThemeChanged += (s, e) => ApplyThemeResources();

            BtnClose.Click += (s, e) => this.Close();
            BtnApplySelected.Click += BtnApplySelected_Click;
            BtnDiscardSelected.Click += BtnDiscardSelected_Click;
            BtnClearFilters.Click += (s, e) => { if (SearchBox!=null) SearchBox.Text = string.Empty; if (ScopeFilter!=null) ScopeFilter.SelectedIndex = 0; if (OperationFilter!=null) OperationFilter.SelectedIndex = 0; if (HistoryTimeRange!=null) HistoryTimeRange.SelectedIndex = 0; if (HistoryType!=null) HistoryType.SelectedIndex = 0; if (HistorySearch!=null) HistorySearch.Text = string.Empty; RefreshViews(); };

            if (RootShell != null)
                RootShell.Loaded += (s, e) => { RefreshViews(); PendingChangesWindow_Loaded(s, e); };

            PendingList.DoubleTapped += (s, e) => Pending_ContextView_Click(s, e);

            // Listen to main window save to surface info here too
            var main = App.Window as MainWindow;
            if (main != null)
            {
                main.Saved += (s, e) => { ShowLocalInfo("Saved."); RefreshViews(); };
            }

            // Scale-aware initial size based on monitor DPI
            WindowHelpers.ResizeForDisplayScale(this, 900, 640);
            this.Activated += (s, e) => WindowHelpers.BringToFront(this);
            this.Closed += (s, e) => { UnsubscribeCollectionChanges(); App.UnregisterWindow(this); };
            App.RegisterWindow(this);

            try { ScaleHelper.Attach(this, ScaleHost, RootShell!); } catch { }

            SubscribeCollectionChanges();
        }

        private void MainTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            bool pendingActive = (MainTabs.SelectedItem as TabViewItem) == PendingTab;
            BtnApplySelected.Visibility = pendingActive ? Visibility.Visible : Visibility.Collapsed;
            BtnDiscardSelected.Visibility = pendingActive ? Visibility.Visible : Visibility.Collapsed;
            BtnApplyAll.Visibility = pendingActive ? Visibility.Visible : Visibility.Collapsed;
            BtnDiscardAll.Visibility = pendingActive ? Visibility.Visible : Visibility.Collapsed;
            BtnReapplySelected.Visibility = pendingActive ? Visibility.Collapsed : Visibility.Visible;
        }

        private void ApplyTabSelectionUi()
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
        private void OnCollectionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            DispatcherQueue.TryEnqueue(() => RefreshViews());
        }

        // Call once on loaded too
        private void PendingChangesWindow_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyTabSelectionUi();
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
            // Save all pending changes
            ApplySelected(PendingChangesService.Instance.Pending.ToArray());
        }

        private async void BtnApplySelected_Click(object sender, RoutedEventArgs e)
        {
            var items = PendingList.SelectedItems;
            if (items == null || items.Count == 0) return;
            var arr = new PendingChange[items.Count];
            for (int i = 0; i < items.Count; i++) arr[i] = (PendingChange)items[i]!;
            ApplySelected(arr);
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

        private void Pending_ContextApply_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is PendingChange c)
            {
                ApplySelected(new[] { c });
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
            if ((sender as FrameworkElement)?.DataContext is not PendingChange c) return;

            var main = App.Window as MainWindow;
            var bundleField = typeof(MainWindow).GetField("_bundle", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var compField = typeof(MainWindow).GetField("_compSource", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var userField = typeof(MainWindow).GetField("_userSource", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var bundle = (AdmxBundle?)bundleField?.GetValue(main);
            var compSrc = (IPolicySource?)compField?.GetValue(main);
            var userSrc = (IPolicySource?)userField?.GetValue(main);
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
            if (Content is FrameworkElement fe) fe.RequestedTheme = App.CurrentTheme;
        }

        private void SetSaving(bool saving)
        {
            try { if (SavingOverlay != null) SavingOverlay.Visibility = saving ? Visibility.Visible : Visibility.Collapsed; } catch { }
        }

        private async void ApplySelected(IEnumerable<PendingChange> items)
        {
            if (items == null) return;
            SetSaving(true);
            var main = App.Window as MainWindow;
            var bundleField = typeof(MainWindow).GetField("_bundle", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var bundle = (AdmxBundle?)bundleField?.GetValue(main);
            if (bundle == null) { SetSaving(false); return; }

            var appliedList = items.ToList();
            bool wroteOk = true; string? writeErr = null;

            // Build POL buffers using Core pipeline
            (string? compBase64, string? userBase64) = (null, null);
            await Task.Run(() =>
            {
                try
                {
                    var requests = appliedList.Select(c => new PolicyChangeRequest
                    {
                        PolicyId = c.PolicyId,
                        Scope = string.Equals(c.Scope, "User", StringComparison.OrdinalIgnoreCase) ? PolicyTargetScope.User : PolicyTargetScope.Machine,
                        DesiredState = c.DesiredState,
                        Options = c.Options
                    }).ToList();
                    var b64 = PolicySavePipeline.BuildLocalGpoBase64(bundle, requests);
                    compBase64 = b64.machineBase64; userBase64 = b64.userBase64;
                }
                catch (Exception ex) { wroteOk = false; writeErr = ex.Message; }
            }).ConfigureAwait(true);

            if (wroteOk)
            {
                var res = await _elevation.WriteLocalGpoBytesAsync(compBase64, userBase64, triggerRefresh: true);
                if (!res.ok) { wroteOk = false; writeErr = res.error; }
            }

            if (wroteOk)
            {
                PendingChangesService.Instance.Applied(appliedList.ToArray());
                try { main?.RefreshLocalSources(); } catch { }
                RefreshViews();
                ChangesAppliedOrDiscarded?.Invoke(this, EventArgs.Empty);
                NotifyApplied(appliedList.Count);
            }
            else
            {
                if (!string.IsNullOrEmpty(writeErr)) ShowLocalInfo("Save failed: " + writeErr);
            }

            SetSaving(false);
        }

        private async void NotifyApplied(int count)
        {
            var msg = count == 1 ? "1 change saved." : $"{count} changes saved.";
            ShowLocalInfo(msg);
            if (App.Window is MainWindow mw)
            {
                try { mw.GetType().GetMethod("ShowInfo", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.Invoke(mw, new object[] { msg, InfoBarSeverity.Success }); } catch { }
            }
            await System.Threading.Tasks.Task.CompletedTask;
        }

        private async void NotifyDiscarded(int count)
        {
            var msg = count == 1 ? "1 change discarded." : $"{count} changes discarded.";
            ShowLocalInfo(msg);
            if (App.Window is MainWindow mw)
            {
                try { mw.GetType().GetMethod("ShowInfo", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.Invoke(mw, new object[] { msg, InfoBarSeverity.Informational }); } catch { }
            }
            await System.Threading.Tasks.Task.CompletedTask;
        }

        private void ExecuteReapply(HistoryRecord h)
        {
            if (h == null || string.IsNullOrEmpty(h.PolicyId)) return;
            var main = App.Window as MainWindow;
            var compSrcField = typeof(MainWindow).GetField("_compSource", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var userSrcField = typeof(MainWindow).GetField("_userSource", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var bundleField = typeof(MainWindow).GetField("_bundle", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var compSrc = (IPolicySource?)compSrcField?.GetValue(main);
            var userSrc = (IPolicySource?)userSrcField?.GetValue(main);
            var bundle = (AdmxBundle?)bundleField?.GetValue(main);
            if (bundle == null || !bundle.Policies.TryGetValue(h.PolicyId, out var pol)) return;
            var src = string.Equals(h.Scope, "User", StringComparison.OrdinalIgnoreCase) ? userSrc : compSrc;
            if (src == null) return;

            PolicyProcessing.ForgetPolicy(src, pol);
            if (h.DesiredState == PolicyState.Enabled)
            {
                PolicyProcessing.SetPolicyState(src, pol, PolicyState.Enabled, h.Options ?? new Dictionary<string, object>());
            }
            else if (h.DesiredState == PolicyState.Disabled)
            {
                PolicyProcessing.SetPolicyState(src, pol, PolicyState.Disabled, new Dictionary<string, object>());
            }

            PendingChangesService.Instance.History.Add(new HistoryRecord
            {
                PolicyId = h.PolicyId,
                PolicyName = h.PolicyName,
                Scope = h.Scope,
                Action = "Reapply",
                Result = "Reapplied",
                Details = h.Details,
                AppliedAt = DateTime.Now,
                DesiredState = h.DesiredState,
                Options = h.Options
            });

            RefreshViews();
            ChangesAppliedOrDiscarded?.Invoke(this, EventArgs.Empty);
            ShowLocalInfo("Reapplied.");
        }

        private void History_ContextReapply_Click(object sender, RoutedEventArgs e)
        { if ((sender as FrameworkElement)?.DataContext is HistoryRecord h) ExecuteReapply(h); }
        private void BtnReapplySelected_Click(object sender, RoutedEventArgs e)
        {
            var items = HistoryList.SelectedItems; if (items == null || items.Count == 0) return;
            for (int i = 0; i < items.Count; i++) if (items[i] is HistoryRecord h) ExecuteReapply(h);
        }

        private async void PendingList_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            if ((sender as ListView)?.SelectedItem is PendingChange c)
            {
                var section = string.Equals(c.Scope, "User", StringComparison.OrdinalIgnoreCase) ? AdmxPolicySection.User : AdmxPolicySection.Machine;
                var main = App.Window as MainWindow; if (main == null) return;
                await main.OpenEditDialogForPolicyIdAsync(c.PolicyId, section, true);
            }
        }

        private async void HistoryList_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            if ((sender as ListView)?.SelectedItem is HistoryRecord h)
            {
                var section = string.Equals(h.Scope, "User", StringComparison.OrdinalIgnoreCase) ? AdmxPolicySection.User : AdmxPolicySection.Machine;
                var main = App.Window as MainWindow; if (main == null) return;
                await main.OpenEditDialogForPolicyIdAsync(h.PolicyId, section, true);
            }
        }
    }
}
