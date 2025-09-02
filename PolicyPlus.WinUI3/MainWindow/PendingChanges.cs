using Microsoft.UI.Xaml;
using PolicyPlus.WinUI3.Services;
using PolicyPlus;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace PolicyPlus.WinUI3
{
    public sealed partial class MainWindow
    {
        private async Task<(bool ok, string? error)> SavePendingAsync(PendingChange[] items)
        {
            if (items == null || items.Length == 0) return (true, null);
            if (_bundle == null) return (false, "No ADMX bundle loaded");

            (string? compBase64, string? userBase64) = (null, null);
            try
            {
                var requests = items.Select(c => new PolicyChangeRequest
                {
                    PolicyId = c.PolicyId,
                    Scope = string.Equals(c.Scope, "User", StringComparison.OrdinalIgnoreCase) ? PolicyTargetScope.User : PolicyTargetScope.Machine,
                    DesiredState = c.DesiredState,
                    Options = c.Options
                }).ToList();
                var b64 = PolicySavePipeline.BuildLocalGpoBase64(_bundle, requests);
                compBase64 = b64.machineBase64; userBase64 = b64.userBase64;
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }

            var res = await ElevationService.Instance.WriteLocalGpoBytesAsync(compBase64, userBase64, triggerRefresh: true);
            return (res.ok, res.error);
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
                    SetBusy(false);

                    if (ok)
                    {
                        PendingChangesService.Instance.Applied(pending);
                        RefreshLocalSources();
                        UpdateUnsavedIndicator();
                        ApplyFiltersAndBind(SearchBox?.Text ?? string.Empty);
                        ShowInfo("Saved.", Microsoft.UI.Xaml.Controls.InfoBarSeverity.Success);
                        try { Saved?.Invoke(this, EventArgs.Empty); } catch { }
                    }
                    else
                    {
                        ShowInfo("Save failed: " + (err ?? "unknown"), Microsoft.UI.Xaml.Controls.InfoBarSeverity.Error);
                    }
                }
            }
            catch
            {
                SetBusy(false);
            }
        }
    }
}
