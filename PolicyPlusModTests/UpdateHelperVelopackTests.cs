using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace PolicyPlusModTests;

public class UpdateHelperVelopackTests
{
    private static Type GetUpdateHelperType()
    {
        // Use a public WinUI3 type to get the assembly, then fetch the internal UpdateHelper
        var asm = typeof(PolicyPlus.WinUI3.App).Assembly;
        var t = asm.GetType("PolicyPlus.WinUI3.Services.UpdateHelper", throwOnError: true)!;
        return t;
    }

    private static bool IsVelopackAvailable(Type t)
    {
        var prop = t.GetProperty("IsVelopackAvailable", BindingFlags.Public | BindingFlags.Static)!;
        return (bool)prop.GetValue(null)!;
    }

    private static async Task<(bool ok, bool restart, string? message)> InvokeCheckAndApplyAsync(Type t)
    {
        var method = t.GetMethod("CheckAndApplyVelopackUpdatesAsync", BindingFlags.Public | BindingFlags.Static)!;
        var taskObj = (Task)method.Invoke(null, null)!;
        await taskObj.ConfigureAwait(false);
        // Task result is a ValueTuple<bool,bool,string?> when Velopack code path compiled; else immediate tuple already returned.
        var taskType = taskObj.GetType();
        if (taskType.IsGenericType)
        {
            var result = taskType.GetProperty("Result")!.GetValue(taskObj)!;
            // Deconstruct via dynamic to avoid reflection on ValueTuple members.
            dynamic dyn = result;
            return ((bool)dyn.Item1, (bool)dyn.Item2, (string?)dyn.Item3);
        }
        // Fallback (should not happen here) ? treat as failure.
        return (false, false, "Unexpected task type");
    }

    private static async Task<bool> InvokeRestartAsync(Type t, bool flag)
    {
        var method = t.GetMethod("RestartIfVelopackRequestedAsync", BindingFlags.Public | BindingFlags.Static)!;
        var taskObj = (Task)method.Invoke(null, new object[] { flag })!;
        await taskObj.ConfigureAwait(false);
        var taskType = taskObj.GetType();
        if (taskType.IsGenericType)
        {
            return (bool)taskType.GetProperty("Result")!.GetValue(taskObj)!;
        }
        return false;
    }

    private static void SetUpdateManager(Type t, object? mgr)
    {
        var field = t.GetField("_updateManager", BindingFlags.NonPublic | BindingFlags.Static);
        if (field == null)
            return; // Velopack code not compiled ? nothing to set.
        field.SetValue(null, mgr);
    }

    [Fact]
    public async Task Velopack_FallbackOrSuccess_WithMock()
    {
        var t = GetUpdateHelperType();
        bool available = IsVelopackAvailable(t);

        if (!available)
        {
            // When Velopack not compiled in, we expect the known fallback tuple.
            var (ok, restart, msg) = await InvokeCheckAndApplyAsync(t);
            Assert.False(ok);
            Assert.False(restart);
            Assert.Equal("Velopack not included", msg);
            return; // No further tests possible.
        }

        // Success path with updates + restart required
        var mgr1 = new FakeUpdateManager(hasUpdates: true, restartRequired: true);
        SetUpdateManager(t, mgr1);
        var (ok1, restart1, msg1) = await InvokeCheckAndApplyAsync(t);
        Assert.True(ok1);
        Assert.True(restart1);
        Assert.Equal("Restart required", msg1);
        Assert.True(mgr1.CheckCalled);
        Assert.True(mgr1.DownloadCalled);
        Assert.True(mgr1.ApplyCalled);

        bool restarted = await InvokeRestartAsync(t, restart1);
        Assert.True(restarted);
        Assert.True(mgr1.RestartCalled);

        // No updates case
        var mgr2 = new FakeUpdateManager(hasUpdates: false, restartRequired: false);
        SetUpdateManager(t, mgr2);
        var (ok2, restart2, msg2) = await InvokeCheckAndApplyAsync(t);
        Assert.True(ok2);
        Assert.False(restart2);
        Assert.Equal("No updates available", msg2);
        Assert.True(mgr2.CheckCalled);
        Assert.False(mgr2.DownloadCalled);
        Assert.False(mgr2.ApplyCalled);
    }

    private sealed class FakeUpdateManager
    {
        private readonly bool _hasUpdates;
        private readonly bool _restartRequired;

        public bool CheckCalled { get; private set; }
        public bool DownloadCalled { get; private set; }
        public bool ApplyCalled { get; private set; }
        public bool RestartCalled { get; private set; }

        public FakeUpdateManager(bool hasUpdates, bool restartRequired)
        {
            _hasUpdates = hasUpdates;
            _restartRequired = restartRequired;
        }

        public Task<FakeUpdateInfo?> CheckForUpdatesAsync()
        {
            CheckCalled = true;
            if (!_hasUpdates)
                return Task.FromResult<FakeUpdateInfo?>(new FakeUpdateInfo(Array.Empty<int>()));
            return Task.FromResult<FakeUpdateInfo?>(new FakeUpdateInfo(new[] { 1 }));
        }

        public Task DownloadUpdatesAsync(FakeUpdateInfo info)
        {
            if (info.Updates.Count > 0)
                DownloadCalled = true;
            return Task.CompletedTask;
        }

        public Task<FakeApplyResult> ApplyUpdatesAsync(FakeUpdateInfo info)
        {
            if (info.Updates.Count > 0)
                ApplyCalled = true;
            return Task.FromResult(new FakeApplyResult(_restartRequired));
        }

        public void RestartApp()
        {
            RestartCalled = true;
        }
    }

    private sealed class FakeUpdateInfo
    {
        public IList Updates { get; }
        public FakeUpdateInfo(IList updates) => Updates = updates;
    }

    private sealed class FakeApplyResult
    {
        public bool RestartRequired { get; }
        public FakeApplyResult(bool restart) => RestartRequired = restart;
    }
}
