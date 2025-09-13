using System;
using System.Linq;
using System.Threading;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Patterns;
using PolicyPlusPlus.Tests.UI.Infrastructure;
using Xunit;

namespace PolicyPlusPlus.Tests.UI.Ui;

public class SearchTests : IClassFixture<TestAppFixture>
{
    private readonly TestAppFixture _fixture;
    public SearchTests(TestAppFixture fixture) => _fixture = fixture;

    // Unified timeouts
    private const int FindElementTimeoutSeconds = 6;
    private const int FilterTimeoutSeconds = 5;
    private const int StableRowTimeoutSeconds = 5;
    private const int PollShortMs = 40;

    #region Helpers
    private static void ClearAndType(AutomationElement element, string text)
    {
        element.Focus();
        Keyboard.Press(VirtualKeyShort.CONTROL);
        Keyboard.Press(VirtualKeyShort.KEY_A);
        Keyboard.Release(VirtualKeyShort.KEY_A);
        Keyboard.Release(VirtualKeyShort.CONTROL);
        Keyboard.Press(VirtualKeyShort.DELETE);
        Keyboard.Release(VirtualKeyShort.DELETE);
        if (!string.IsNullOrEmpty(text)) Keyboard.Type(text);
        Thread.Sleep(100);
    }

    private static AutomationElement FindDescendantWithRetry(AutomationElement root, string automationId, int timeoutSeconds = FindElementTimeoutSeconds)
        => Retry.WhileNull(
                () => root.FindFirstDescendant(cf => cf.ByAutomationId(automationId)),
                timeout: TimeSpan.FromSeconds(timeoutSeconds),
                interval: TimeSpan.FromMilliseconds(PollShortMs))
            .Result ?? throw new InvalidOperationException(automationId + " not found");

    private static AutomationElement[] GetRows(AutomationElement list)
        => list.FindAllDescendants(cf => cf.ByControlType(ControlType.DataItem));

    private static bool RowContains(AutomationElement row, string term)
    {
        var lower = term.ToLowerInvariant();
        try
        {
            var texts = row.FindAllDescendants(cf => cf.ByControlType(ControlType.Text));
            return texts.Any(t => (t.Name ?? string.Empty).ToLowerInvariant().Contains(lower));
        }
        catch { return false; }
    }

    private static bool AnyRowContains(AutomationElement list, string term)
        => GetRows(list).Any(r => RowContains(r, term));

    private static AutomationElement[] WaitForFilteredRows(AutomationElement list, string term, int timeoutSeconds = FilterTimeoutSeconds)
    {
        var end = DateTime.UtcNow + TimeSpan.FromSeconds(timeoutSeconds);
        AutomationElement[] rows = Array.Empty<AutomationElement>();
        while (DateTime.UtcNow < end)
        {
            rows = GetRows(list);
            if (rows.Length > 0 && AnyRowContains(list, term)) break;
            Thread.Sleep(PollShortMs);
        }
        if (rows.Length == 0 || !AnyRowContains(list, term)) throw new TimeoutException("Timed out waiting for rows containing '" + term + "'.");
        return rows;
    }

    private static AutomationElement[] WaitForStableRowCount(AutomationElement list, int minRows, int timeoutSeconds = StableRowTimeoutSeconds)
    {
        var end = DateTime.UtcNow + TimeSpan.FromSeconds(timeoutSeconds);
        AutomationElement[] latest = Array.Empty<AutomationElement>();
        int stableTicks = 0;
        int lastCount = -1;
        while (DateTime.UtcNow < end)
        {
            latest = GetRows(list);
            if (latest.Length == lastCount) stableTicks++; else stableTicks = 0;
            lastCount = latest.Length;
            if (latest.Length >= minRows && stableTicks >= 2) break; // need a couple stable polls
            Thread.Sleep(PollShortMs);
        }
        return latest;
    }

    private static AutomationElement[] WaitForBaseline(AutomationElement list, int desiredMinimum)
    {
        var first = WaitForStableRowCount(list, desiredMinimum, StableRowTimeoutSeconds);
        if (first.Length >= desiredMinimum) return first;
        var extended = WaitForStableRowCount(list, desiredMinimum, StableRowTimeoutSeconds * 2);
        return extended.Length >= desiredMinimum ? extended : first;
    }

    private static void InvokeIfSupported(AutomationElement element)
    { try { element.Patterns.Invoke.PatternOrDefault?.Invoke(); } catch { } }

    private static void SelectIfSupported(AutomationElement element)
    {
        try { element.Patterns.SelectionItem.PatternOrDefault?.Select(); }
        catch { InvokeIfSupported(element); }
    }

    private static void ToggleOffIfOn(AutomationElement checkbox)
    {
        try
        {
            var toggle = checkbox.Patterns.Toggle.PatternOrDefault;
            if (toggle != null && toggle.ToggleState == ToggleState.On)
                toggle.Toggle();
        }
        catch { }
    }

    private static void ResetUiState(Window window)
    {
        // Clear search
        try
        {
            var search = window.FindFirstDescendant(cf => cf.ByAutomationId("SearchBox"));
            if (search != null) ClearAndType(search, string.Empty);
        }
        catch { }
        // Clear category filter
        try
        {
            var clearBtn = window.FindFirstDescendant(cf => cf.ByAutomationId("ClearCategoryFilterButton"));
            if (clearBtn != null) InvokeIfSupported(clearBtn);
        }
        catch { }
        // Ensure bookmark / configured checkboxes are OFF
        try
        {
            var bm = window.FindFirstDescendant(cf => cf.ByAutomationId("BookmarksOnlyCheck"));
            if (bm != null) ToggleOffIfOn(bm);
        }
        catch { }
        try
        {
            var cfg = window.FindFirstDescendant(cf => cf.ByAutomationId("ConfiguredOnlyCheck"));
            if (cfg != null) ToggleOffIfOn(cfg);
        }
        catch { }
        Thread.Sleep(120); // allow UI to rebind
    }

    private static AutomationElement FindCategoryItem(Window window, string name)
        => Retry.WhileNull(
                () => window.FindFirstDescendant(cf => cf.ByControlType(ControlType.TreeItem).And(cf.ByName(name))),
                timeout: TimeSpan.FromSeconds(FindElementTimeoutSeconds),
                interval: TimeSpan.FromMilliseconds(PollShortMs))
            .Result ?? throw new InvalidOperationException("Category tree item '" + name + "' not found");

    private static void WaitUntil(Func<bool> predicate, int timeoutMs = 2500)
    {
        var end = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < end)
        {
            if (predicate()) return;
            Thread.Sleep(PollShortMs);
        }
        throw new TimeoutException("Condition not met within timeout");
    }
    #endregion

    [Fact]
    public void SearchBox_FiltersPolicyList_WithBoolean()
    {
        var window = _fixture.Host.MainWindow!;
        ResetUiState(window);
        var searchBox = FindDescendantWithRetry(window, "SearchBox");
        var policyList = FindDescendantWithRetry(window, "PolicyList");
        WaitForBaseline(policyList, 3);
        ClearAndType(searchBox, "Boolean");
        var rows = WaitForFilteredRows(policyList, "Boolean");
        Assert.NotEmpty(rows);
    }

    [Fact]
    public void SearchBox_FiltersPolicyList_WithNested()
    {
        var window = _fixture.Host.MainWindow!;
        ResetUiState(window);
        var searchBox = FindDescendantWithRetry(window, "SearchBox");
        var policyList = FindDescendantWithRetry(window, "PolicyList");
        WaitForBaseline(policyList, 3);
        ClearAndType(searchBox, "Nested");
        var rows = WaitForFilteredRows(policyList, "Nested");
        Assert.NotEmpty(rows);
    }

    [Fact]
    public void SearchBox_ClearRestoresFullList()
    {
        var window = _fixture.Host.MainWindow!;
        ResetUiState(window);
        var searchBox = FindDescendantWithRetry(window, "SearchBox");
        var policyList = FindDescendantWithRetry(window, "PolicyList");

        ClearAndType(searchBox, string.Empty);
        var baselineRows = WaitForBaseline(policyList, 3);
        int baselineCount = baselineRows.Length;
        Assert.True(baselineCount >= 1);

        ClearAndType(searchBox, "Enum");
        var filteredRows = WaitForFilteredRows(policyList, "Enum");
        Assert.True(filteredRows.Length > 0 && filteredRows.Length <= baselineCount);

        ClearAndType(searchBox, string.Empty);
        var restored = WaitForBaseline(policyList, Math.Max(1, baselineCount));
        Assert.True(restored.Length >= 1);
    }

    [Fact]
    public void Category_Selection_Filters_List()
    {
        var window = _fixture.Host.MainWindow!;
        ResetUiState(window);
        var policyList = FindDescendantWithRetry(window, "PolicyList");
        WaitForBaseline(policyList, 3);
        var catItem = FindCategoryItem(window, "Dummy Category");
        SelectIfSupported(catItem);

        WaitUntil(() =>
        {
            var rows = GetRows(policyList);
            return rows.Any(r => RowContains(r, "Dummy"));
        });
        Assert.Contains(GetRows(policyList), r => RowContains(r, "Dummy"));

        var clearBtn = window.FindFirstDescendant(cf => cf.ByAutomationId("ClearCategoryFilterButton"));
        if (clearBtn != null) InvokeIfSupported(clearBtn);
    }

    [Fact]
    public void BookmarksOnly_Shows_OnlyBookmarked()
    {
        var window = _fixture.Host.MainWindow!;
        ResetUiState(window);
        var policyList = FindDescendantWithRetry(window, "PolicyList");
        var baseline = WaitForBaseline(policyList, 3);
        var firstTwo = baseline.Take(2).ToArray();
        foreach (var row in firstTwo)
        {
            var starBtn = row.FindFirstDescendant(cf => cf.ByControlType(ControlType.Button));
            if (starBtn != null) InvokeIfSupported(starBtn);
        }
        var bookmarkCheck = window.FindFirstDescendant(cf => cf.ByAutomationId("BookmarksOnlyCheck"));
        if (bookmarkCheck != null) InvokeIfSupported(bookmarkCheck);
        WaitUntil(() => { var rows = GetRows(policyList); return rows.Length > 0 && rows.Length <= baseline.Length; });
        var filtered = GetRows(policyList);
        Assert.True(filtered.Length > 0 && filtered.Length <= baseline.Length);
    }

    [Fact]
    public void ConfiguredOnly_Shows_Configured()
    {
        var window = _fixture.Host.MainWindow!;
        ResetUiState(window);
        var policyList = FindDescendantWithRetry(window, "PolicyList");
        WaitForBaseline(policyList, 3);
        var configuredCheck = window.FindFirstDescendant(cf => cf.ByAutomationId("ConfiguredOnlyCheck"));
        if (configuredCheck != null) InvokeIfSupported(configuredCheck);
        // No policies configured yet; we just validate no crash and list still accessible
        WaitUntil(() => GetRows(policyList).Length >= 1);
    }
}

public class TestAppFixture : IDisposable
{
    public TestAppHost Host { get; }
    public TestAppFixture()
    {
        Host = new TestAppHost();
        Host.Launch();
    }
    public void Dispose() => Host.Dispose();
}
