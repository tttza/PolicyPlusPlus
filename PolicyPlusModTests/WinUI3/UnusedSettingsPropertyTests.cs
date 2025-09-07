using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Xunit;
using PolicyPlus.WinUI3.Services;

namespace PolicyPlusModTests.WinUI3
{
    public class UnusedSettingsPropertyTests
    {
        private static readonly Regex PropAccessRegex = new(@"\.([A-Za-z_][A-Za-z0-9_]*)", RegexOptions.Compiled);

        private string GetSolutionRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "PolicyPlusMod.sln")))
                dir = dir.Parent;
            if (dir == null) throw new InvalidOperationException("Solution root not found");
            return dir.FullName;
        }

        [Fact]
        public void AppSettings_AllPropertiesAreReferencedSomewhere()
        {
            var props = typeof(AppSettings).GetProperties(BindingFlags.Instance|BindingFlags.Public)
                                           .Select(p => p.Name)
                                           .ToList();

            var root = GetSolutionRoot();
            var csFiles = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
                                    .Where(p => !p.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar) &&
                                                !p.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
                                    .ToList();

            var referenced = new HashSet<string>(StringComparer.Ordinal);
            foreach (var file in csFiles)
            {
                string text;
                try { text = File.ReadAllText(file); }
                catch { continue; }
                foreach (Match m in PropAccessRegex.Matches(text))
                {
                    var name = m.Groups[1].Value;
                    if (props.Contains(name)) referenced.Add(name);
                }
            }

            var missing = props.Where(p => !referenced.Contains(p)).ToList();

            Assert.True(missing.Count == 0, "Unreferenced AppSettings properties: " + string.Join(", ", missing));
        }

        [Fact]
        public void ColumnsOptions_AllPropertiesAreReferencedSomewhere()
        {
            var props = typeof(ColumnsOptions).GetProperties(BindingFlags.Instance|BindingFlags.Public)
                                              .Select(p => p.Name)
                                              .ToList();
            var root = GetSolutionRoot();
            var csFiles = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
                                    .Where(p => !p.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar) &&
                                                !p.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
                                    .ToList();

            var referenced = new HashSet<string>(StringComparer.Ordinal);
            foreach (var file in csFiles)
            {
                string text;
                try { text = File.ReadAllText(file); }
                catch { continue; }
                foreach (Match m in PropAccessRegex.Matches(text))
                {
                    var name = m.Groups[1].Value;
                    if (props.Contains(name)) referenced.Add(name);
                }
            }

            var missing = props.Where(p => !referenced.Contains(p)).ToList();
            Assert.True(missing.Count == 0, "Unreferenced ColumnsOptions properties: " + string.Join(", ", missing));
        }
    }
}
