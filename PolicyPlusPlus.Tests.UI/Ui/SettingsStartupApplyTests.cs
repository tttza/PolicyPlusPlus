using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using PolicyPlusPlus.Tests.UI.Infrastructure;
using Xunit;

namespace PolicyPlusPlus.Tests.UI.Ui;

public class SettingsStartupApplyTests
{
    private static AutomationElement? TryFind(
        AutomationElement root,
        string name,
        ControlType? type
    )
    {
        try
        {
            if (type.HasValue)
            {
                return root.FindFirstDescendant(cf =>
                        cf.ByName(name).And(cf.ByControlType(type.Value))
                    )
                    ?? root.FindFirstDescendant(cf =>
                        cf.ByControlType(type.Value).And(cf.ByName(name))
                    );
            }
            return root.FindFirstDescendant(cf => cf.ByName(name));
        }
        catch
        {
            return null;
        }
    }

    private static AutomationElement WaitFor(
        AutomationElement root,
        string name,
        ControlType? type = null,
        int timeoutMs = 4000
    )
    {
        var start = Environment.TickCount;
        while (Environment.TickCount - start < timeoutMs)
        {
            var el = TryFind(root, name, type);
            if (el != null)
                return el;
            Thread.Sleep(80);
        }
        throw new InvalidOperationException("Element not found: " + name);
    }

    private static bool IsChecked(AutomationElement el)
    {
        try
        {
            return el.Patterns.Toggle.Pattern.ToggleState.Value
                == FlaUI.Core.Definitions.ToggleState.On;
        }
        catch
        {
            return false;
        }
    }

    [Fact]
    public void PreSeededSettings_AllStartupApplied()
    {
        var dir = Path.Combine(
            Path.GetTempPath(),
            "PolicyPlusUITest_PreSeed_" + System.Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(dir);
        var settings = new
        {
            ConfiguredOnly = true,
            BookmarksOnly = true,
            HideEmptyCategories = true,
            ShowDetails = false,
        };
        File.WriteAllText(Path.Combine(dir, "settings.json"), JsonSerializer.Serialize(settings));

        using var host = new TestAppHost(dir);
        host.Launch();
        var window = host.MainWindow!;

        Assert.True(IsChecked(WaitFor(window, "Configured only", ControlType.CheckBox)));
        Assert.True(IsChecked(WaitFor(window, "Bookmarks only", ControlType.CheckBox)));

        OpenTopMenu(window, "Options");
        Assert.True(IsChecked(WaitFor(window, "Hide empty categories")));

        OpenTopMenu(window, "Appearance");
        Assert.False(IsChecked(WaitFor(window, "Details pane")));
    }

    private static void OpenTopMenu(AutomationElement window, string title)
    {
        var item = WaitFor(window, title);
        try
        {
            item.Patterns.Invoke.Pattern.Invoke();
        }
        catch
        {
            item.Click();
        }
        Thread.Sleep(150);
    }
}
