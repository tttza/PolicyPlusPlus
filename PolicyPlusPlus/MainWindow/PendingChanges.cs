using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using PolicyPlusPlus.Logging; // logging
using PolicyPlusPlus.Services;
using PolicyPlusPlus.Utils;

namespace PolicyPlusPlus
{
    public sealed partial class MainWindow
    {
        private async Task<(bool ok, string? error)> SavePendingAsync(PendingChange[] items)
        {
            if (items == null || items.Length == 0)
            {
                Log.Debug("MainSave", "No pending items");
                return (true, null);
            }
            if (_bundle == null)
            {
                Log.Error("MainSave", "Bundle not loaded");
                return (false, "No ADMX bundle loaded");
            }
            var mgr = PolicySourceManager.Instance;
            (bool ok, string? err) result = await mgr.ApplyPendingAsync(
                _bundle,
                items,
                new ElevationServiceAdapter()
            );
            return (result.ok, result.err);
        }

        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var pending = PendingChangesService.Instance.Pending.ToArray();
                if (pending.Length > 0)
                {
                    SetBusy(true, "Saving...");
                    var (ok, err) = await SavePendingAsync(pending);
                    if (ok)
                    {
                        PendingChangesService.Instance.Applied(pending);
                        PolicySourceManager.Instance.Refresh();
                        try
                        {
                            var affected = pending
                                .Select(p => p.PolicyId)
                                .Distinct(StringComparer.OrdinalIgnoreCase)
                                .ToList();
                            EventHub.PublishPolicySourcesRefreshed(affected);
                            EventHub.PublishPendingAppliedOrDiscarded(affected);
                        }
                        catch { }
                        RefreshVisibleRows();
                        UpdateUnsavedIndicator();
                        ApplyFiltersAndBind(SearchBox?.Text ?? string.Empty);
                        if (!string.IsNullOrEmpty(err))
                        {
                            ShowInfo(
                                MessageFormatHelper.FormatRefreshFailureMessage(pending.Length, err),
                                Microsoft.UI.Xaml.Controls.InfoBarSeverity.Warning
                            );
                        }
                        else
                        {
                            ShowInfo("Saved.", Microsoft.UI.Xaml.Controls.InfoBarSeverity.Success);
                        }
                        try
                        {
                            Saved?.Invoke(this, EventArgs.Empty);
                        }
                        catch { }
                    }
                    else
                    {
                        ShowInfo(
                            "Save failed: " + (err ?? "unknown"),
                            Microsoft.UI.Xaml.Controls.InfoBarSeverity.Error
                        );
                    }
                }
            }
            finally
            {
                try
                {
                    SetBusy(false);
                }
                catch { }
            }
        }
    }
}
