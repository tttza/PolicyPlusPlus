using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PolicyPlusCore.Core;

public sealed class AdmxWatcher : IAsyncDisposable
{
    private readonly FileSystemWatcher _watchLocal;
    private readonly TimeSpan _debounce = TimeSpan.FromMilliseconds(500);
    private readonly ConcurrentDictionary<string, DateTimeOffset> _pending = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly Func<IReadOnlyCollection<string>, Task> _onBatch;

    public AdmxWatcher(string localPath, Func<IReadOnlyCollection<string>, Task> onBatch)
    {
        _onBatch = onBatch;
        _watchLocal = new FileSystemWatcher(localPath, "*.adm*")
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
        };
        _watchLocal.Changed += OnFs;
        _watchLocal.Created += OnFs;
        _watchLocal.Renamed += OnFs;
        _watchLocal.Deleted += OnFs;
        _watchLocal.EnableRaisingEvents = true;

        _ = Task.Run(ProcessLoopAsync);
    }

    private void OnFs(object sender, FileSystemEventArgs e)
    {
        if (
            e.FullPath.EndsWith(".admx", StringComparison.OrdinalIgnoreCase)
            || e.FullPath.EndsWith(".adml", StringComparison.OrdinalIgnoreCase)
        )
        {
            _pending[e.FullPath] = DateTimeOffset.UtcNow;
        }
    }

    private async Task ProcessLoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            await Task.Delay(_debounce).ConfigureAwait(false);
            var now = DateTimeOffset.UtcNow;
            var ready = _pending
                .Where(kv => now - kv.Value > _debounce)
                .Select(kv => kv.Key)
                .ToArray();
            if (ready.Length == 0)
                continue;
            foreach (var k in ready)
                _pending.TryRemove(k, out _);
            try
            {
                await _onBatch(ready).ConfigureAwait(false);
            }
            catch { }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _watchLocal.EnableRaisingEvents = false;
        _watchLocal.Dispose();
        await Task.CompletedTask;
    }
}
