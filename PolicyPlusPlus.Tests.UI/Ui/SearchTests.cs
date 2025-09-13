using System;
using System.Linq;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI; // For VirtualKeyShort
using FlaUI.Core.AutomationElements; // For AutomationElement
using PolicyPlusPlus.Tests.UI.Infrastructure;
using Xunit;

namespace PolicyPlusPlus.Tests.UI.Ui;

// Basic smoke test for search function. Further tests can expand scenarios.
public class SearchTests : IClassFixture<TestAppFixture>
{
    private readonly TestAppFixture _fixture;
    public SearchTests(TestAppFixture fixture) => _fixture = fixture;

    [Fact]
    public void SearchBox_FiltersPolicyList()
    {
        var window = _fixture.Host.MainWindow!;

        // Find search box via automation id (poll until available)
        var searchBox = Retry.WhileNull(
            () => window.FindFirstDescendant(cf => cf.ByAutomationId("SearchBox")),
            timeout: TimeSpan.FromSeconds(10)).Result
            ?? throw new InvalidOperationException("SearchBox not found");

        searchBox.Focus();

        // Clear any existing text: Ctrl+A -> Delete
        Keyboard.Press(VirtualKeyShort.CONTROL);
        Keyboard.Press(VirtualKeyShort.KEY_A);
        Keyboard.Release(VirtualKeyShort.KEY_A);
        Keyboard.Release(VirtualKeyShort.CONTROL);
        Keyboard.Press(VirtualKeyShort.DELETE);
        Keyboard.Release(VirtualKeyShort.DELETE);

        // Type a short query that should return results
        Keyboard.Type("firewall");
        Keyboard.Press(VirtualKeyShort.RETURN);
        Keyboard.Release(VirtualKeyShort.RETURN);

        // Acquire PolicyList with polling
        var policyList = Retry.WhileNull(
            () => window.FindFirstDescendant(cf => cf.ByAutomationId("PolicyList")),
            timeout: TimeSpan.FromSeconds(10)).Result
            ?? throw new InvalidOperationException("PolicyList not found");

        // Poll rows until at least one non-blank row appears (or timeout)
        var rows = Retry.While(
            () => policyList.FindAllDescendants(cf => cf.ByControlType(ControlType.DataItem)),
            r => r == null || r.Length == 0 || r.Any(x => string.IsNullOrWhiteSpace(x.Name)),
            timeout: TimeSpan.FromSeconds(15),
            throwOnTimeout: true).Result ?? Array.Empty<AutomationElement>();

        Assert.True(rows.Length > 0, "Expected at least one search result row");
        var anyBlank = rows.Any(r => string.IsNullOrWhiteSpace(r.Name));
        Assert.False(anyBlank, "Found row with blank name");
    }

    [Fact]
    public void SearchBox_ClearRestoresFullList()
    {
        var window = _fixture.Host.MainWindow!;

        // Find search box via automation id (poll until available)
        var searchBox = Retry.WhileNull(
            () => window.FindFirstDescendant(cf => cf.ByAutomationId("SearchBox")),
            timeout: TimeSpan.FromSeconds(10)).Result
            ?? throw new InvalidOperationException("SearchBox not found");

        searchBox.Focus();

        // Ensure empty search (full list)
        Keyboard.Press(VirtualKeyShort.CONTROL);
        Keyboard.Press(VirtualKeyShort.KEY_A);
        Keyboard.Release(VirtualKeyShort.KEY_A);
        Keyboard.Release(VirtualKeyShort.CONTROL);
        Keyboard.Press(VirtualKeyShort.DELETE);
        Keyboard.Release(VirtualKeyShort.DELETE);
        Keyboard.Press(VirtualKeyShort.RETURN); // commit empty query if needed
        Keyboard.Release(VirtualKeyShort.RETURN);

        // Acquire PolicyList with polling
        var policyList = Retry.WhileNull(
            () => window.FindFirstDescendant(cf => cf.ByAutomationId("PolicyList")),
            timeout: TimeSpan.FromSeconds(10)).Result
            ?? throw new InvalidOperationException("PolicyList not found");

        // Get the baseline row count
        var baselineRows = Retry.While(
            () => policyList.FindAllDescendants(cf => cf.ByControlType(ControlType.DataItem)),
            r => r == null || r.Length == 0 || r.Any(x => string.IsNullOrWhiteSpace(x.Name)),
            timeout: TimeSpan.FromSeconds(20),
            throwOnTimeout: true).Result ?? Array.Empty<AutomationElement>();

        Assert.True(baselineRows.Length > 0, "Expected baseline rows before filtering");

        // Perform a filter search
        Keyboard.Type("firewall");
        Keyboard.Press(VirtualKeyShort.RETURN);
        Keyboard.Release(VirtualKeyShort.RETURN);

        var filteredRows = Retry.While(
            () => policyList.FindAllDescendants(cf => cf.ByControlType(ControlType.DataItem)),
            r => r == null || r.Length == 0 || r.Any(x => string.IsNullOrWhiteSpace(x.Name)),
            timeout: TimeSpan.FromSeconds(15),
            throwOnTimeout: true).Result ?? Array.Empty<AutomationElement>();

        Assert.True(filteredRows.Length > 0, "Expected some rows after filtering");

        // Clear search again
        Keyboard.Press(VirtualKeyShort.CONTROL);
        Keyboard.Press(VirtualKeyShort.KEY_A);
        Keyboard.Release(VirtualKeyShort.KEY_A);
        Keyboard.Release(VirtualKeyShort.CONTROL);
        Keyboard.Press(VirtualKeyShort.DELETE);
        Keyboard.Release(VirtualKeyShort.DELETE);
        Keyboard.Press(VirtualKeyShort.RETURN);
        Keyboard.Release(VirtualKeyShort.RETURN);

        // Wait until row count returns (>= baseline)
        var restoredRows = Retry.While(
            () => policyList.FindAllDescendants(cf => cf.ByControlType(ControlType.DataItem)),
            r => r == null || r.Length < baselineRows.Length || r.Any(x => string.IsNullOrWhiteSpace(x.Name)),
            timeout: TimeSpan.FromSeconds(20),
            throwOnTimeout: true).Result ?? Array.Empty<AutomationElement>();

        Assert.True(restoredRows.Length >= baselineRows.Length, "Expected restored list size to be at least baseline size");
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
