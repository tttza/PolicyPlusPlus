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

namespace PolicyPlus.WinUI3.Windows
{
    public sealed partial class PendingChangesWindow : Window
    {
        public static event EventHandler? ChangesAppliedOrDiscarded;

        private List<PendingChange> _pendingView = new();
        private List<HistoryRecord> _historyView = new();

        public PendingChangesWindow()
        {
            InitializeComponent();
            Title = "Pending changes";

            ApplyThemeResources();
            App.ThemeChanged += (s, e) => ApplyThemeResources();

            BtnClose.Click += (s, e) => this.Close();
            BtnApplySelected.Click += BtnApplySelected_Click;
            BtnDiscardSelected.Click += BtnDiscardSelected_Click;
            BtnClearFilters.Click += (s, e) => { if (SearchBox!=null) SearchBox.Text = string.Empty; if (ScopeFilter!=null) ScopeFilter.SelectedIndex = 0; if (OperationFilter!=null) OperationFilter.SelectedIndex = 0; if (HistoryTimeRange!=null) HistoryTimeRange.SelectedIndex = 0; if (HistoryType!=null) HistoryType.SelectedIndex = 0; if (HistorySearch!=null) HistorySearch.Text = string.Empty; RefreshViews(); };
            BtnExportPending.Click += BtnExportPending_Click;

            if (RootGrid != null)
                RootGrid.Loaded += (s, e) => RefreshViews();

            PendingList.DoubleTapped += (s, e) => Pending_ContextView_Click(s, e);

            // Listen to main window save to surface info here too
            var main = App.Window as MainWindow;
            if (main != null)
            {
                main.Saved += (s, e) => ShowLocalInfo("Saved.");
            }

            WindowHelpers.Resize(this, 900, 640);
            this.Activated += (s, e) => WindowHelpers.BringToFront(this);
            this.Closed += (s, e) => App.UnregisterWindow(this);
            App.RegisterWindow(this);
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
            _pendingView = ApplyPendingFilters(srcPending);
            if (PendingList != null) PendingList.ItemsSource = _pendingView;

            var srcHistory = PendingChangesService.Instance.History?.ToList() ?? new List<HistoryRecord>();
            _historyView = ApplyHistoryFilters(srcHistory);
            if (HistoryList != null) HistoryList.ItemsSource = _historyView;

            UpdateSummary();
        }

        private List<PendingChange> ApplyPendingFilters(IEnumerable<PendingChange> src)
        {
            string q = (SearchBox?.Text ?? string.Empty).Trim();
            string scope = ((ScopeFilter?.SelectedItem as ComboBoxItem)?.Content?.ToString()) ?? "Both";
            string op = ((OperationFilter?.SelectedItem as ComboBoxItem)?.Content?.ToString()) ?? "All";

            var list = new List<PendingChange>();
            foreach (var c in src)
            {
                if (c == null) continue;
                bool scopeOk = (scope == "Both") || string.Equals(c.Scope ?? string.Empty, scope, StringComparison.OrdinalIgnoreCase);
                bool opOk = (op == "All") || string.Equals(c.Action ?? string.Empty, op, StringComparison.OrdinalIgnoreCase);
                bool textOk = string.IsNullOrEmpty(q) ||
                              (c.PolicyName?.Contains(q, StringComparison.OrdinalIgnoreCase) == true) ||
                              (c.Details?.Contains(q, StringComparison.OrdinalIgnoreCase) == true);
                if (scopeOk && opOk && textOk)
                    list.Add(c);
            }
            return list.OrderBy(c => c.PolicyName ?? string.Empty).ToList();
        }

        private List<HistoryRecord> ApplyHistoryFilters(IEnumerable<HistoryRecord> src)
        {
            string q = (HistorySearch?.Text ?? string.Empty).Trim();
            string type = ((HistoryType?.SelectedItem as ComboBoxItem)?.Content?.ToString()) ?? "All";
            string range = ((HistoryTimeRange?.SelectedItem as ComboBoxItem)?.Content?.ToString()) ?? "All";

            DateTime? since = null;
            if (string.Equals(range, "Today", StringComparison.OrdinalIgnoreCase)) since = DateTime.Today;
            else if (string.Equals(range, "Last 7 days", StringComparison.OrdinalIgnoreCase)) since = DateTime.Today.AddDays(-7);
            else if (string.Equals(range, "Last 30 days", StringComparison.OrdinalIgnoreCase)) since = DateTime.Today.AddDays(-30);

            var list = new List<HistoryRecord>();
            foreach (var h in src)
            {
                if (h == null) continue;
                bool typeOk = (type == "All") || string.Equals(h.Result ?? string.Empty, type, StringComparison.OrdinalIgnoreCase);
                bool sinceOk = (!since.HasValue) || h.AppliedAt >= since.Value;
                bool textOk = string.IsNullOrEmpty(q) ||
                              (h.PolicyName?.Contains(q, StringComparison.OrdinalIgnoreCase) == true) ||
                              (h.Details?.Contains(q, StringComparison.OrdinalIgnoreCase) == true);
                if (typeOk && sinceOk && textOk)
                    list.Add(h);
            }
            return list.OrderByDescending(h => h.AppliedAt).ToList();
        }

        private void UpdateSummary()
        {
            if (SummaryText != null)
            {
                var count = PendingChangesService.Instance.Pending.Count;
                SummaryText.Text = count == 0 ? "No pending changes" : $"{count} pending change(s)";
            }
        }

        private void BtnApplySelected_Click(object sender, RoutedEventArgs e)
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

        private async void BtnExportPending_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                var picker = new FileSavePicker();
                InitializeWithWindow.Initialize(picker, hwnd);
                picker.FileTypeChoices.Add("CSV", new List<string> { ".csv" });
                picker.SuggestedFileName = "pending";
                var file = await picker.PickSaveFileAsync();
                if (file == null) return;

                var rows = new List<string>();
                rows.Add("PolicyId,PolicyName,Scope,Action,Details");
                foreach (var c in _pendingView)
                {
                    string esc(string s) => '"' + (s ?? string.Empty).Replace("\"", "\"\"") + '"';
                    rows.Add(string.Join(",", new[] { esc(c.PolicyId), esc(c.PolicyName), esc(c.Scope), esc(c.Action), esc(c.Details) }));
                }
                await global::Windows.Storage.FileIO.WriteLinesAsync(file, rows);
            }
            catch { }
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

        private void BtnApplyAll_Click(object sender, RoutedEventArgs e)
        {
            ApplySelected(PendingChangesService.Instance.Pending.ToArray());
        }

        private void BtnDiscardAll_Click(object sender, RoutedEventArgs e)
        {
            int count = PendingChangesService.Instance.Pending.Count;
            PendingChangesService.Instance.DiscardAll();
            RefreshViews();
            ChangesAppliedOrDiscarded?.Invoke(this, EventArgs.Empty);
            NotifyDiscarded(count);
        }

        private void ApplyThemeResources()
        {
            if (Content is FrameworkElement fe) fe.RequestedTheme = App.CurrentTheme;
        }

        private void ApplySelected(IEnumerable<PendingChange> items)
        {
            if (items == null) return;
            var main = App.Window as MainWindow;
            var compSrcField = typeof(MainWindow).GetField("_compSource", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var userSrcField = typeof(MainWindow).GetField("_userSource", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var bundleField = typeof(MainWindow).GetField("_bundle", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var compSrc = (IPolicySource?)compSrcField?.GetValue(main);
            var userSrc = (IPolicySource?)userSrcField?.GetValue(main);
            var bundle = (AdmxBundle?)bundleField?.GetValue(main);
            if (bundle == null) return;

            var appliedList = items.ToList();
            foreach (var c in appliedList)
            {
                if (string.IsNullOrEmpty(c.PolicyId)) continue;
                if (!bundle.Policies.TryGetValue(c.PolicyId, out var pol)) continue;
                var src = string.Equals(c.Scope, "User", StringComparison.OrdinalIgnoreCase) ? userSrc : compSrc;
                if (src == null) continue;
                PolicyProcessing.ForgetPolicy(src, pol);
                if (c.DesiredState == PolicyState.Enabled)
                {
                    PolicyProcessing.SetPolicyState(src, pol, PolicyState.Enabled, c.Options);
                }
                else if (c.DesiredState == PolicyState.Disabled)
                {
                    PolicyProcessing.SetPolicyState(src, pol, PolicyState.Disabled, null);
                }
            }

            PendingChangesService.Instance.Applied(appliedList.ToArray());
            RefreshViews();
            ChangesAppliedOrDiscarded?.Invoke(this, EventArgs.Empty);
            NotifyApplied(appliedList.Count);
        }

        private async void NotifyApplied(int count)
        {
            var msg = count == 1 ? "1 change applied." : $"{count} changes applied.";
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
    }
}
