using System;
using System.Linq;
using System.Threading;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;
using FlaUI.Core.AutomationElements;
using PolicyPlusPlus.Tests.UI.Infrastructure;
using Xunit;

namespace PolicyPlusPlus.Tests.UI.Ui;

public class SearchTests : IClassFixture<TestAppFixture>
{
    private readonly TestAppFixture _fixture;
    public SearchTests(TestAppFixture fixture) => _fixture = fixture;

    private const int FindElementTimeoutSeconds = 5;
    private const int FilterTimeoutSeconds = 8;
    private const int StableRowTimeoutSeconds = 8;
    private const int PollShortMs = 55;

    private static void SetValue(AutomationElement element, string text)
    {
        try
        {
            var pattern = element.Patterns.Value.Pattern;
            pattern.SetValue(text);
        }
        catch
        {
            element.Focus();
            Keyboard.Press(VirtualKeyShort.CONTROL);
            Keyboard.Press(VirtualKeyShort.KEY_A);
            Keyboard.Release(VirtualKeyShort.KEY_A);
            Keyboard.Release(VirtualKeyShort.CONTROL);
            Keyboard.Press(VirtualKeyShort.DELETE);
            Keyboard.Release(VirtualKeyShort.DELETE);
            if (!string.IsNullOrEmpty(text)) Keyboard.Type(text);
        }
        Thread.Sleep(70);
    }

    private static AutomationElement FindDescendantWithRetry(AutomationElement root, string automationId, int timeoutSeconds = FindElementTimeoutSeconds)
    {
        return Retry.WhileNull(
            () => root.FindFirstDescendant(cf => cf.ByAutomationId(automationId)),
            timeout: TimeSpan.FromSeconds(timeoutSeconds),
            interval: TimeSpan.FromMilliseconds(PollShortMs)
        ).Result ?? throw new InvalidOperationException(automationId + " not found");
    }

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
        while (DateTime.UtcNow < end)
        {
            latest = GetRows(list);
            if (latest.Length >= minRows) break;
            Thread.Sleep(PollShortMs);
        }
        if (latest.Length < minRows) throw new TimeoutException($"Timed out waiting for >= {minRows} rows (had {latest.Length}).");
        return latest;
    }

    private static void CommitSearch()
    {
        Keyboard.Press(VirtualKeyShort.RETURN);
        Keyboard.Release(VirtualKeyShort.RETURN);
        Thread.Sleep(90);
    }

    [Fact]
    public void SearchBox_FiltersPolicyList_WithBoolean()
    {
        var window = _fixture.Host.MainWindow!;
        var searchBox = FindDescendantWithRetry(window, "SearchBox");
        var policyList = FindDescendantWithRetry(window, "PolicyList");
        SetValue(searchBox, "Boolean");
        CommitSearch();
        var rows = WaitForFilteredRows(policyList, "Boolean");
        Assert.True(rows.Length > 0, "Expected results for 'Boolean'");
        Assert.True(AnyRowContains(policyList, "Boolean"));
    }

    [Fact]
    public void SearchBox_FiltersPolicyList_WithNested()
    {
        var window = _fixture.Host.MainWindow!;
        var searchBox = FindDescendantWithRetry(window, "SearchBox");
        var policyList = FindDescendantWithRetry(window, "PolicyList");
        SetValue(searchBox, "Nested");
        CommitSearch();
        var rows = WaitForFilteredRows(policyList, "Nested");
        Assert.True(rows.Length > 0, "Expected results for 'Nested'");
        Assert.True(AnyRowContains(policyList, "Nested"));
    }

    [Fact]
    public void SearchBox_ClearRestoresFullList()
    {
        var window = _fixture.Host.MainWindow!;
        var searchBox = FindDescendantWithRetry(window, "SearchBox");
        var policyList = FindDescendantWithRetry(window, "PolicyList");

        SetValue(searchBox, string.Empty);
        CommitSearch();
        var baselineRows = WaitForStableRowCount(policyList, minRows: 5);
        int baselineCount = baselineRows.Length;
        Assert.True(baselineCount >= 5);

        SetValue(searchBox, "Enum");
        CommitSearch();
        var filteredRows = WaitForFilteredRows(policyList, "Enum");
        Assert.True(filteredRows.Length > 0 && filteredRows.Length <= baselineCount);
        Assert.True(AnyRowContains(policyList, "Enum"));

        SetValue(searchBox, string.Empty);
        CommitSearch();
        var restored = WaitForStableRowCount(policyList, minRows: baselineCount);
        Assert.True(restored.Length >= baselineCount);
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
