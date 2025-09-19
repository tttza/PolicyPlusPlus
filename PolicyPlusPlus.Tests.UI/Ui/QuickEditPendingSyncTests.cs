using System;
using System.Linq;
using System.Threading;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;
using PolicyPlusPlus.Tests.UI.Infrastructure;
using Xunit;

namespace PolicyPlusPlus.Tests.UI.Ui;

public class QuickEditPendingSyncTests : IClassFixture<TestAppFixture>
{
    private readonly TestAppFixture _fixture;

    public QuickEditPendingSyncTests(TestAppFixture fixture) => _fixture = fixture;

    private const int TimeoutMs = 9000;
    private const int PollMs = 120;

    private static AutomationElement FindByAutomationId(AutomationElement root, string id) =>
        Retry
            .WhileNull(
                () => root.FindFirstDescendant(cf => cf.ByAutomationId(id)),
                timeout: TimeSpan.FromMilliseconds(TimeoutMs),
                interval: TimeSpan.FromMilliseconds(PollMs)
            )
            .Result ?? throw new InvalidOperationException("AutomationId not found: " + id);

    private static AutomationElement[] GetRows(AutomationElement list) =>
        list.FindAllDescendants(cf => cf.ByControlType(ControlType.DataItem));

    private static bool RowContains(AutomationElement row, string text)
    {
        try
        {
            var target = text.ToLowerInvariant();
            var parts = row.FindAllDescendants(cf => cf.ByControlType(ControlType.Text));
            return parts.Any(p => (p.Name ?? string.Empty).ToLowerInvariant().Contains(target));
        }
        catch
        {
            return false;
        }
    }

    private static AutomationElement WaitForPolicyRow(AutomationElement list, string term)
    {
        var end = DateTime.UtcNow.AddMilliseconds(TimeoutMs);
        while (DateTime.UtcNow < end)
        {
            var rows = GetRows(list);
            var match = rows.FirstOrDefault(r => RowContains(r, term));
            if (match != null)
                return match;
            Thread.Sleep(PollMs);
        }
        throw new TimeoutException("Policy row not found: " + term);
    }

    private static void InvokePattern(AutomationElement el)
    {
        try
        {
            el.Patterns.Invoke.Pattern.Invoke();
        }
        catch { }
    }

    private static void SendCtrlS()
    {
        Keyboard.Press(VirtualKeyShort.CONTROL);
        Keyboard.Press(VirtualKeyShort.KEY_S);
        Keyboard.Release(VirtualKeyShort.KEY_S);
        Keyboard.Release(VirtualKeyShort.CONTROL);
    }

    private static void SendEnter()
    {
        Keyboard.Press(VirtualKeyShort.RETURN);
        Keyboard.Release(VirtualKeyShort.RETURN);
    }

    private static bool UnsavedIndicatorVisible(AutomationElement window)
    {
        try
        {
            var unsaved = window.FindFirstDescendant(cf => cf.ByAutomationId("UnsavedIndicator"));
            if (unsaved == null)
                return false;
            return !unsaved.Properties.IsOffscreen.Value;
        }
        catch
        {
            return false;
        }
    }

    private AutomationElement OpenEditWindowRobust(
        AutomationElement desktop,
        AutomationElement list,
        AutomationElement row
    )
    {
        var end = DateTime.UtcNow.AddMilliseconds(TimeoutMs);
        AutomationElement? editWin = null;
        for (int attempt = 0; attempt < 6 && DateTime.UtcNow < end; attempt++)
        {
            try
            {
                list.Focus();
            }
            catch { }
            Thread.Sleep(80);
            try
            {
                row.Focus();
            }
            catch { }
            try
            {
                row.Patterns.SelectionItem.Pattern.Select();
            }
            catch { }
            if (attempt == 0)
                InvokePattern(row); // first: invoke
            else if (attempt % 2 == 1)
            {
                try
                {
                    row.DoubleClick();
                }
                catch { }
            }
            else
                SendEnter();
            Thread.Sleep(250 + attempt * 100);
            editWin = desktop
                .FindAllChildren()
                .FirstOrDefault(c =>
                    (c.Name ?? string.Empty).Contains(
                        "Edit Policy Setting",
                        StringComparison.OrdinalIgnoreCase
                    )
                );
            if (editWin != null)
                break;
        }
        return editWin!; // caller asserts
    }

    [Fact]
    public void SaveFromMainWindow_QuickEditAndPendingReflectApplied()
    {
        var main = _fixture.Host.MainWindow!;
        var desktop = _fixture.Host.Automation!.GetDesktop();
        var list = FindByAutomationId(main, "PolicyList");

        var row = WaitForPolicyRow(list, "Boolean Policy");
        var editWin = OpenEditWindowRobust(desktop, list, row);
        Assert.NotNull(editWin);

        var enabled = editWin.FindFirstDescendant(cf => cf.ByName("Enabled"));
        Assert.NotNull(enabled);
        InvokePattern(enabled);
        var okBtn = editWin.FindFirstDescendant(cf => cf.ByName("OK"));
        Assert.NotNull(okBtn);
        InvokePattern(okBtn);

        Assert.True(
            Retry
                .WhileFalse(
                    () => UnsavedIndicatorVisible(main),
                    timeout: TimeSpan.FromMilliseconds(5000),
                    interval: TimeSpan.FromMilliseconds(150)
                )
                .Success
        );

        var unsaved = main.FindFirstDescendant(cf => cf.ByAutomationId("UnsavedIndicator"));
        Assert.NotNull(unsaved);
        try
        {
            unsaved.Click();
        }
        catch
        {
            InvokePattern(unsaved);
        }

        AutomationElement pendingWin = null!;
        var end = DateTime.UtcNow.AddMilliseconds(TimeoutMs);
        while (DateTime.UtcNow < end)
        {
            var maybePending = desktop
                .FindAllChildren()
                .FirstOrDefault(c =>
                    (c.Name ?? string.Empty).Contains(
                        "Pending changes",
                        StringComparison.OrdinalIgnoreCase
                    )
                );
            if (maybePending != null)
            {
                pendingWin = maybePending;
                break;
            }
            Thread.Sleep(180);
        }
        Assert.NotNull(pendingWin);

        SendCtrlS();

        Assert.True(
            Retry
                .WhileTrue(
                    () => UnsavedIndicatorVisible(main),
                    timeout: TimeSpan.FromMilliseconds(6000),
                    interval: TimeSpan.FromMilliseconds(200)
                )
                .Success,
            "Unsaved indicator still visible after save."
        );

        bool PendingListCleared()
        {
            try
            {
                var listEl = pendingWin.FindFirstDescendant(cf => cf.ByAutomationId("PendingList"));
                if (listEl == null)
                    return false;
                var items = listEl.FindAllDescendants(cf => cf.ByControlType(ControlType.DataItem));
                return items.Length == 0;
            }
            catch
            {
                return false;
            }
        }
        Assert.True(
            Retry
                .WhileTrue(
                    () => !PendingListCleared(),
                    timeout: TimeSpan.FromMilliseconds(6000),
                    interval: TimeSpan.FromMilliseconds(250)
                )
                .Success,
            "Pending list not cleared after save."
        );
    }
}
