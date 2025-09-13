using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using PolicyPlusPlus.Tests.UI.Infrastructure;
using Xunit;

namespace PolicyPlusPlus.Tests.UI.Ui;

public class SettingsPersistenceTests : IClassFixture<TestAppFixture>
{
    private readonly TestAppFixture _fixture;
    public SettingsPersistenceTests(TestAppFixture fixture) => _fixture = fixture;

    private const int ShortTimeoutMs = 3000; // slightly shorter
    private const int PopupTimeoutMs = 4000;
    private const int PollMs = 25; // faster polling

    private static AutomationElement? TryFind(AutomationElement root, string name, ControlType? type)
    {
        try
        {
            if (type.HasValue)
            {
                return root.FindFirstDescendant(cf => cf.ByName(name).And(cf.ByControlType(type.Value)))
                    ?? root.FindFirstDescendant(cf => cf.ByControlType(type.Value).And(cf.ByName(name)));
            }
            return root.FindFirstDescendant(cf => cf.ByName(name));
        }
        catch { return null; }
    }

    private AutomationElement? GlobalFindByName(string name)
    {
        try
        {
            var desktop = _fixture.Host.Automation?.GetDesktop();
            if (desktop == null) return null;
            var exact = desktop.FindFirstDescendant(cf => cf.ByName(name));
            if (exact != null) return exact;
            var lowered = name.ToLowerInvariant();
            return desktop.FindAllDescendants().FirstOrDefault(e => (e.Name ?? string.Empty).ToLowerInvariant().Contains(lowered));
        }
        catch { return null; }
    }

    private AutomationElement WaitForElementByName(AutomationElement root, string name, ControlType? type = null, int timeoutMs = ShortTimeoutMs)
    {
        var start = Environment.TickCount;
        AutomationElement? el = null;
        int attempt = 0;
        while (Environment.TickCount - start < timeoutMs)
        {
            attempt++;
            el = TryFind(root, name, type);
            if (el != null) return el;
            if (attempt > 4)
            {
                el = GlobalFindByName(name);
                if (el != null) return el;
            }
            Thread.Sleep(PollMs);
        }
        throw new InvalidOperationException("Element not found: " + name);
    }

    private AutomationElement WaitForGlobal(string name, int timeoutMs = PopupTimeoutMs)
    {
        var start = Environment.TickCount;
        AutomationElement? el = null;
        while (Environment.TickCount - start < timeoutMs)
        {
            el = GlobalFindByName(name);
            if (el != null) return el;
            Thread.Sleep(PollMs);
        }
        throw new InvalidOperationException("Element not found globally: " + name);
    }

    private static void Invoke(AutomationElement el)
    {
        try { el.Patterns.Invoke.Pattern.Invoke(); }
        catch
        {
            bool expanded = false;
            try { el.Patterns.ExpandCollapse.Pattern.Expand(); expanded = true; } catch { }
            if (!expanded)
            {
                try { el.Click(); }
                catch { throw new InvalidOperationException("Element not invokable: " + (el.Name ?? el.AutomationId)); }
            }
        }
        Thread.Sleep(45);
    }

    private static void Toggle(AutomationElement el)
    {
        try { el.Patterns.Toggle.Pattern.Toggle(); }
        catch { Invoke(el); return; }
        Thread.Sleep(45);
    }

    private static bool IsChecked(AutomationElement el)
    {
        try { return el.Patterns.Toggle.Pattern.ToggleState.Value == FlaUI.Core.Definitions.ToggleState.On; }
        catch { return false; }
    }

    private static DateTime? GetSettingsLastWrite(string dir)
    {
        try
        {
            var p = Path.Combine(dir, "settings.json");
            return File.Exists(p) ? File.GetLastWriteTimeUtc(p) : null;
        }
        catch { return null; }
    }

    private static void WaitForSettingsWrite(string dir, ref DateTime? lastWrite, int maxWaitMs = 800)
    {
        var p = Path.Combine(dir, "settings.json");
        var start = Environment.TickCount;
        while (Environment.TickCount - start < maxWaitMs)
        {
            try
            {
                if (File.Exists(p))
                {
                    var t = File.GetLastWriteTimeUtc(p);
                    if (!lastWrite.HasValue || t > lastWrite.Value)
                    {
                        lastWrite = t;
                        return;
                    }
                }
            }
            catch { }
            Thread.Sleep(40);
        }
    }

    private static string ReadSettingsJsonFast(string dir)
    {
        var p = Path.Combine(dir, "settings.json");
        var start = Environment.TickCount;
        while (!File.Exists(p) && Environment.TickCount - start < 1500) Thread.Sleep(30);
        Assert.True(File.Exists(p), "settings.json not found at " + p);
        return File.ReadAllText(p);
    }

    private string? _openTopMenu;

    private void OpenTopMenu(AutomationElement window, string title)
    {
        if (string.Equals(_openTopMenu, title, StringComparison.OrdinalIgnoreCase)) return;
        AutomationElement item;
        try { item = WaitForElementByName(window, title, ControlType.MenuItem); }
        catch { item = GlobalFindByName(title) ?? WaitForElementByName(window, title, null); }
        Invoke(item);
        _openTopMenu = title;
        Thread.Sleep(60);
    }

    private AutomationElement EnsureSubMenu(string parentMenuTitle, string subItemName)
    {
        // Always re-open parent (reset cache) to guarantee fresh popup then find sub item
        _openTopMenu = null;
        OpenTopMenu(_fixture.Host.MainWindow!, parentMenuTitle);
        // Try within window first
        try { return WaitForElementByName(_fixture.Host.MainWindow!, subItemName, ControlType.MenuItem, PopupTimeoutMs); }
        catch { }
        // Fallback global
        return WaitForGlobal(subItemName);
    }

    private void TryCloseCustomPolDialog()
    {
        try
        {
            var desktop = _fixture.Host.Automation?.GetDesktop(); if (desktop == null) return;
            // Dialog title defined in XAML: "Custom POL Settings"
            var dlg = desktop.FindFirstDescendant(cf => cf.ByName("Custom POL Settings"));
            if (dlg == null) return;
            // Primary button text = OK
            var okBtn = dlg.FindFirstDescendant(cf => cf.ByName("OK")) ?? dlg.FindFirstDescendant(cf => cf.ByControlType(ControlType.Button));
            if (okBtn != null)
            {
                Invoke(okBtn);
                Thread.Sleep(120); // brief wait for dialog close and settings write
            }
        }
        catch { }
    }

    [Fact]
    public void ToggleSettings_PersistWrites()
    {
        var window = _fixture.Host.MainWindow!;
        var writeTime = GetSettingsLastWrite(_fixture.Host.TestDataDirectory);

        var configuredChk = WaitForElementByName(window, "Configured only", ControlType.CheckBox);
        bool initialConfigured = IsChecked(configuredChk);
        Toggle(configuredChk); WaitForSettingsWrite(_fixture.Host.TestDataDirectory, ref writeTime);

        var bookmarksChk = WaitForElementByName(window, "Bookmarks only", ControlType.CheckBox);
        bool initialBookmarks = IsChecked(bookmarksChk);
        Toggle(bookmarksChk); WaitForSettingsWrite(_fixture.Host.TestDataDirectory, ref writeTime);

        // Options menu related toggles
        OpenTopMenu(window, "Options");
        var hideItem = WaitForElementByName(window, "Hide empty categories", null, timeoutMs: PopupTimeoutMs);
        bool initialHide = IsChecked(hideItem);
        Toggle(hideItem); WaitForSettingsWrite(_fixture.Host.TestDataDirectory, ref writeTime);

        // Custom pol is under File, not Options
        OpenTopMenu(window, "File");
        AutomationElement? useCustomPol = null;
        try { useCustomPol = WaitForElementByName(window, "Use custom .pol", null, timeoutMs: 600); } catch { }
        bool? initialUseCustom = useCustomPol != null ? IsChecked(useCustomPol) : null;
        if (useCustomPol != null)
        {
            Toggle(useCustomPol);
            // Dialog appears if first activation or not configured -> close it via OK
            TryCloseCustomPolDialog();
            WaitForSettingsWrite(_fixture.Host.TestDataDirectory, ref writeTime);
        }

        // Appearance menu related
        OpenTopMenu(window, "Appearance");
        var detailsItem = WaitForElementByName(window, "Details pane", null, timeoutMs: PopupTimeoutMs);
        bool initialDetails = IsChecked(detailsItem);
        Toggle(detailsItem); WaitForSettingsWrite(_fixture.Host.TestDataDirectory, ref writeTime);

        var themeRoot = EnsureSubMenu("Appearance", "Theme");
        Invoke(themeRoot);
        var darkTheme = WaitForGlobal("Dark");
        var lightTheme = WaitForGlobal("Light");
        string currentTheme = IsChecked(darkTheme) ? "Dark" : (IsChecked(lightTheme) ? "Light" : "System");
        string targetTheme = currentTheme == "Dark" ? "Light" : "Dark";
        var targetThemeEl = targetTheme == "Dark" ? darkTheme : lightTheme;
        if (!IsChecked(targetThemeEl)) { Toggle(targetThemeEl); WaitForSettingsWrite(_fixture.Host.TestDataDirectory, ref writeTime); }

        var scaleRoot = EnsureSubMenu("Appearance", "Scale");
        Invoke(scaleRoot);
        var scale110 = WaitForGlobal("110%");
        var scale90 = WaitForGlobal("90%");
        string targetScale;
        if (IsChecked(scale110)) { targetScale = "90%"; Toggle(scale90); }
        else { targetScale = "110%"; Toggle(scale110); }
        WaitForSettingsWrite(_fixture.Host.TestDataDirectory, ref writeTime);

        var json = ReadSettingsJsonFast(_fixture.Host.TestDataDirectory);
        using var doc = JsonDocument.Parse(json);
        bool? configuredOnly = doc.RootElement.TryGetProperty("ConfiguredOnly", out var coProp) && coProp.ValueKind == JsonValueKind.True ? true : (coProp.ValueKind == JsonValueKind.False ? false : null);
        bool? bookmarksOnly = doc.RootElement.TryGetProperty("BookmarksOnly", out var boProp) && boProp.ValueKind == JsonValueKind.True ? true : (boProp.ValueKind == JsonValueKind.False ? false : null);
        bool? hideEmpty = doc.RootElement.TryGetProperty("HideEmptyCategories", out var hProp) && hProp.ValueKind == JsonValueKind.True ? true : (hProp.ValueKind == JsonValueKind.False ? false : null);
        bool? showDetails = doc.RootElement.TryGetProperty("ShowDetails", out var sdProp) && sdProp.ValueKind == JsonValueKind.True ? true : (sdProp.ValueKind == JsonValueKind.False ? false : null);
        string? theme = doc.RootElement.TryGetProperty("Theme", out var tProp) && tProp.ValueKind == JsonValueKind.String ? tProp.GetString() : null;
        string? scale = doc.RootElement.TryGetProperty("UIScale", out var sProp) && sProp.ValueKind == JsonValueKind.String ? sProp.GetString() : null;

        bool? customPolActive = null;
        if (doc.RootElement.TryGetProperty("CustomPol", out var cpProp) && cpProp.ValueKind == JsonValueKind.Object)
        {
            if (cpProp.TryGetProperty("Active", out var actProp))
            {
                if (actProp.ValueKind == JsonValueKind.True) customPolActive = true;
                else if (actProp.ValueKind == JsonValueKind.False) customPolActive = false;
            }
        }

        Assert.Equal(!initialConfigured, configuredOnly ?? initialConfigured);
        Assert.Equal(!initialBookmarks, bookmarksOnly ?? initialBookmarks);
        Assert.Equal(!initialHide, hideEmpty ?? initialHide);
        Assert.Equal(!initialDetails, showDetails ?? initialDetails);
        Assert.Equal(targetTheme, theme);
        Assert.Equal(targetScale, scale);
        if (initialUseCustom.HasValue && customPolActive.HasValue && initialUseCustom.Value)
        {
            Assert.False(customPolActive.Value);
        }
    }
}
