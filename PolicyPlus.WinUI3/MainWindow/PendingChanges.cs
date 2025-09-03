using Microsoft.UI.Xaml;
using PolicyPlus.WinUI3.Services;
using System;
using System.Linq;
using System.Threading.Tasks;
using PolicyPlus.Core.Core;

namespace PolicyPlus.WinUI3
{
    public sealed partial class MainWindow
    {
        private async Task<(bool ok, string? error)> SavePendingAsync(PendingChange[] items)
        {
            if (items == null || items.Length == 0) return (true, null);
            if (_bundle == null) return (false, "No ADMX bundle loaded");

            // Use the same coordinator and timeout as PendingChangesWindow to avoid UI hangs
            var (ok, error, _, _) = await Services.SaveChangesCoordinator.SaveAsync(
                _bundle,
                items,
                new ElevationServiceAdapter(),
                TimeSpan.FromSeconds(8),
                triggerRefresh: true);
            return (ok, error);
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
            finally
            {
                try { SetBusy(false); } catch { }
            }
        }
    }
}
