using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace PolicyPlusModTests.WinUI3
{
    public class XamlHandlerConsistencyTests
    {
        private static readonly string[] EventAttributeNames = new[]
        {
            // Common WinUI / XAML event names
            "Click",
            "Loaded",
            "SelectionChanged",
            "Tapped",
            "DoubleTapped",
            "PointerReleased",
            "PointerPressed",
            "PointerEntered",
            "PointerExited",
            "KeyDown",
            "RightTapped",
            "TextChanged",
            "QuerySubmitted",
            "SuggestionChosen",
            "Checked",
            "Unchecked",
            "Invoked",
            "Sorting",
            "ItemInvoked",
            "PointerMoved",
            "PointerCanceled",
            "PointerCaptureLost",
            "LayoutUpdated",
        };

        private static readonly Regex CSharpIdentifier = new Regex(
            @"^[A-Za-z_][A-Za-z0-9_]*$",
            RegexOptions.Compiled
        );

        private string GetSolutionRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "PolicyPlusMod.sln")))
                dir = dir.Parent;
            if (dir == null)
                throw new InvalidOperationException("Solution root not found");
            return dir.FullName;
        }

        [Fact]
        public void AllXamlEventHandlersHaveBackingMethods()
        {
            var root = GetSolutionRoot();
            var winUiProject = Path.Combine(root, "PolicyPlusPlus");
            var xamlFiles = Directory
                .GetFiles(winUiProject, "*.xaml", SearchOption.AllDirectories)
                .Where(p => !p.EndsWith("App.xaml", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var assembly = typeof(PolicyPlusPlus.App).Assembly; // target assembly
            var missing = new List<string>();

            foreach (var xamlPath in xamlFiles)
            {
                string text = File.ReadAllText(xamlPath);
                // Quick skip if no event attribute markers
                if (!EventAttributeNames.Any(n => text.Contains(n + "=\"")))
                    continue;
                XDocument doc;
                try
                {
                    doc = XDocument.Load(xamlPath, LoadOptions.PreserveWhitespace);
                }
                catch
                {
                    continue;
                } // skip malformed (unlikely)

                var rootElement = doc.Root;
                if (rootElement == null)
                    continue;
                var classAttr = rootElement
                    .Attributes()
                    .FirstOrDefault(a =>
                        a.Name.LocalName == "Class" || a.Name.LocalName == "ClassName"
                    );
                if (classAttr == null)
                    continue; // resource dictionaries etc.
                var className = classAttr.Value.Trim();
                var type = assembly.GetType(className);
                if (type == null)
                    continue; // possibly design-only

                var handlerNames = new HashSet<string>(StringComparer.Ordinal);
                foreach (var el in rootElement.DescendantsAndSelf())
                {
                    foreach (var attr in el.Attributes())
                    {
                        var localName = attr.Name.LocalName;
                        if (!EventAttributeNames.Contains(localName))
                            continue;
                        var value = attr.Value.Trim();
                        if (!CSharpIdentifier.IsMatch(value))
                            continue; // ignore bindings etc.
                        handlerNames.Add(value);
                    }
                }

                if (handlerNames.Count == 0)
                    continue;

                var methods = new HashSet<string>(
                    type.GetMethods(
                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                        )
                        .Select(m => m.Name),
                    StringComparer.Ordinal
                );

                foreach (var h in handlerNames)
                {
                    if (!methods.Contains(h))
                        missing.Add($"{Path.GetFileName(xamlPath)} => {className}.{h}");
                }
            }

            Assert.True(
                missing.Count == 0,
                "Missing XAML handlers:\n" + string.Join("\n", missing)
            );
        }
    }
}
