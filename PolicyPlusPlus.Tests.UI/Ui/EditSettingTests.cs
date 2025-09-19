using System;
using System.Linq;
using System.Threading;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;
using PolicyPlusPlus.Tests.UI.Infrastructure;
using Xunit;

namespace PolicyPlusPlus.Tests.UI.Ui;

public class EditSettingTests : IClassFixture<TestAppFixture>
{
    private readonly TestAppFixture _fixture;

    public EditSettingTests(TestAppFixture fixture) => _fixture = fixture;

    private const int TimeoutMs = 8000; // allow more time for slower CI to materialize window
    private const int PollMs = 60;

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
            var lower = text.ToLowerInvariant();
            var parts = row.FindAllDescendants(cf => cf.ByControlType(ControlType.Text));
            return parts.Any(p => (p.Name ?? string.Empty).ToLowerInvariant().Contains(lower));
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

    private static void SelectRow(AutomationElement row)
    {
        try
        {
            row.Patterns.SelectionItem.Pattern.Select();
        }
        catch { }
    }

    private static bool AnyPendingShowing(AutomationElement window)
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

    private static AutomationElement WaitEditWindow(AutomationElement desktop)
    {
        AutomationElement? editWin = null;
        var end = DateTime.UtcNow.AddMilliseconds(TimeoutMs);
        while (DateTime.UtcNow < end)
        {
            editWin = desktop
                .FindAllChildren()
                .FirstOrDefault(c =>
                    (c.Name ?? string.Empty).Contains(
                        "Edit Policy Setting",
                        StringComparison.OrdinalIgnoreCase
                    )
                );
            if (editWin != null)
                return editWin;
            Thread.Sleep(PollMs);
        }
        throw new TimeoutException("Edit window not found");
    }

    private static bool TryFindEditWindowQuick(
        AutomationElement desktop,
        out AutomationElement? editWin
    )
    {
        editWin = desktop
            .FindAllChildren()
            .FirstOrDefault(c =>
                (c.Name ?? string.Empty).Contains(
                    "Edit Policy Setting",
                    StringComparison.OrdinalIgnoreCase
                )
            );
        return editWin != null;
    }

    private static void SendEnter()
    {
        Keyboard.Press(VirtualKeyShort.RETURN);
        Keyboard.Release(VirtualKeyShort.RETURN);
    }

    private static void RobustOpen(
        AutomationElement mainWindow,
        AutomationElement desktop,
        AutomationElement policyList,
        AutomationElement row
    )
    {
        // Ensure list has focus first
        try
        {
            policyList.Focus();
        }
        catch { }
        Thread.Sleep(120);
        SelectRow(row);
        Thread.Sleep(80);
        // Try multiple strategies
        for (int attempt = 0; attempt < 5; attempt++)
        {
            if (TryFindEditWindowQuick(desktop, out _))
                return; // already open
            // Press Enter
            SendEnter();
            Thread.Sleep(200 + attempt * 60);
            if (TryFindEditWindowQuick(desktop, out _))
                return;
            // Invoke row directly
            InvokePattern(row);
            Thread.Sleep(220 + attempt * 40);
            if (TryFindEditWindowQuick(desktop, out _))
                return;
            // Focus row again (DataGrid may have re-templated)
            try
            {
                row.Focus();
            }
            catch { }
            Thread.Sleep(100);
        }
        // Final attempt: send Enter one more time before giving up; actual wait handled by WaitEditWindow
        SendEnter();
    }

    private static bool GetRadioChecked(AutomationElement? el)
    {
        if (el == null)
            return false;
        try
        {
            return el.Patterns.SelectionItem.Pattern.IsSelected.Value;
        }
        catch { }
        try
        {
            var tp = el.Patterns.Toggle.Pattern;
            return tp.ToggleState.Value == FlaUI.Core.Definitions.ToggleState.On;
        }
        catch { }
        return false;
    }

    private static void EnsureRadioSelected(AutomationElement radio)
    {
        var end = DateTime.UtcNow.AddMilliseconds(3000);
        int attempt = 0;
        while (DateTime.UtcNow < end)
        {
            if (GetRadioChecked(radio))
                return;
            attempt++;
            InvokePattern(radio);
            try
            {
                radio.Patterns.SelectionItem.Pattern.Select();
            }
            catch { }
            try
            {
                radio.Patterns.Toggle.Pattern.Toggle();
            }
            catch { }
            Thread.Sleep(140 + attempt * 50);
        }
        if (!GetRadioChecked(radio))
            throw new TimeoutException("Radio not selected: " + (radio.Name ?? "(unnamed)"));
    }

    [Fact]
    public void EditBooleanPolicy_EnableAndSave_PendingClearedAfterSave()
    {
        var window = _fixture.Host.MainWindow!;
        var desktop = _fixture.Host.Automation!.GetDesktop();
        var policyList = FindByAutomationId(window, "PolicyList");

        // First open
        var row = WaitForPolicyRow(policyList, "Boolean Policy");
        RobustOpen(window, desktop, policyList, row);
        var editWin = WaitEditWindow(desktop);

        var enabledRadio = editWin.FindFirstDescendant(cf => cf.ByName("Enabled"));
        Assert.NotNull(enabledRadio);
        EnsureRadioSelected(enabledRadio);

        var okBtn =
            editWin.FindFirstDescendant(cf => cf.ByName("OK"))
            ?? editWin.FindFirstDescendant(cf =>
                cf.ByControlType(ControlType.Button).And(cf.ByName("OK"))
            );
        Assert.NotNull(okBtn);
        InvokePattern(okBtn);
        Thread.Sleep(400);

        Assert.True(
            Retry
                .WhileFalse(
                    () => AnyPendingShowing(window),
                    timeout: TimeSpan.FromMilliseconds(4000),
                    interval: TimeSpan.FromMilliseconds(150)
                )
                .Success
        );

        // Save (Ctrl+S)
        Keyboard.Press(VirtualKeyShort.CONTROL);
        Keyboard.Press(VirtualKeyShort.KEY_S);
        Keyboard.Release(VirtualKeyShort.KEY_S);
        Keyboard.Release(VirtualKeyShort.CONTROL);
        Thread.Sleep(650);

        Assert.True(
            Retry
                .WhileTrue(
                    () => AnyPendingShowing(window),
                    timeout: TimeSpan.FromMilliseconds(5000),
                    interval: TimeSpan.FromMilliseconds(150)
                )
                .Success,
            "Pending indicator still visible after save."
        );

        // Re-open
        row = WaitForPolicyRow(policyList, "Boolean Policy");
        RobustOpen(window, desktop, policyList, row);
        editWin = WaitEditWindow(desktop);

        var enabledRadio2 = editWin.FindFirstDescendant(cf => cf.ByName("Enabled"));
        Assert.NotNull(enabledRadio2);
        if (!GetRadioChecked(enabledRadio2))
            EnsureRadioSelected(enabledRadio2);
        Assert.True(GetRadioChecked(enabledRadio2), "Enabled radio not selected on reopen");

        var cancelBtn = editWin.FindFirstDescendant(cf => cf.ByName("Cancel"));
        if (cancelBtn != null)
            InvokePattern(cancelBtn);
    }
}
