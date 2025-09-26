using System;
using System.Threading;
using System.Threading.Tasks;
using PolicyPlusCore.Core; // For AdmxCache concrete type cast
using PolicyPlusPlus.Logging;

namespace PolicyPlusPlus.Services;

// Schedules a one-off stale cache purge after startup during an idle window.
internal sealed class CacheMaintenanceScheduler
{
    private static readonly CacheMaintenanceScheduler _instance = new();
    public static CacheMaintenanceScheduler Instance => _instance;

    private volatile bool _started;
    private CancellationTokenSource? _cts;
    private int _attempts;
    private int _executed; // 0 = not yet
    private DateTime _lastUserInputUtc = DateTime.UtcNow;
    private readonly object _gate = new();

    public void NotifyUserInput()
    {
        _lastUserInputUtc = DateTime.UtcNow;
    }

    public void Start()
    {
        lock (_gate)
        {
            if (_started)
                return;
            _started = true;
            _cts = new CancellationTokenSource();
        }
        _ = RunLoopAsync(_cts.Token);
    }

    public void Stop()
    {
        lock (_gate)
        {
            try
            {
                _cts?.Cancel();
            }
            catch { }
        }
    }

    private async Task RunLoopAsync(CancellationToken ct)
    {
        // Load settings; swallow failures (non-fatal for maintenance)
        try
        {
            _ = SettingsService.Instance.LoadSettings();
        }
        catch { }
        // Future settings for enabling/disabling stale purge could be added; currently always enabled.
        int days = 30; // fixed threshold for now
        int initialDelaySec = 120; // 2 min
        int idleSec = 15;
        int retrySec = 60;
        const int maxAttempts = 6;
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(initialDelaySec), ct).ConfigureAwait(false);
        }
        catch
        {
            return;
        }
        while (!ct.IsCancellationRequested && _attempts < maxAttempts && _executed == 0)
        {
            _attempts++;
            if (CanRun(idleSec))
            {
                int removed = 0;
                try
                {
                    Log.Debug(
                        "CacheMaint",
                        $"starting stale purge (thresholdDays={days}, attempt={_attempts})"
                    );
                    // Only available on concrete implementation; avoid extending public interface for now.
                    if (AdmxCacheHostService.Instance.Cache is PolicyPlusCore.Core.AdmxCache impl)
                    {
                        removed = await impl.PurgeStaleCacheEntriesAsync(
                                TimeSpan.FromDays(days),
                                ct
                            )
                            .ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    Log.Warn("CacheMaint", "stale purge failed", ex);
                }
                _executed = 1;
                Log.Debug("CacheMaint", $"stale purge finished removed={removed}");
                Log.Info("CacheMaint", $"stale purge complete removed={removed}");
                break;
            }
            else
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(retrySec), ct).ConfigureAwait(false);
                }
                catch
                {
                    break;
                }
            }
        }
    }

    private bool CanRun(int idleSec)
    {
        if (AdmxCacheHostService.Instance.IsRebuilding)
            return false;
        var idleFor = DateTime.UtcNow - _lastUserInputUtc;
        if (idleFor < TimeSpan.FromSeconds(idleSec))
            return false;
        return true;
    }
}
