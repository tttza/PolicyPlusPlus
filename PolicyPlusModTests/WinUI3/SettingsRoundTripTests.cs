using System;
using System.IO;
using System.Linq;
using Xunit;
using PolicyPlusPlus.Services;

namespace PolicyPlusModTests.WinUI3
{
    public class SettingsRoundTripTests
    {
        private string CreateTempDir()
        {
            string d = Path.Combine(Path.GetTempPath(), "PolicyPlusModTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(d);
            return d;
        }

        [Fact]
        public void ColumnsOptions_RoundTrip_PersistsFlags()
        {
            var dir = CreateTempDir();
            SettingsService.Instance.InitializeForTests(dir);

            // initial save
            var cols = new ColumnsOptions
            {
                ShowId = true,
                ShowCategory = true,
                ShowTopCategory = true,
                ShowCategoryPath = true,
                ShowApplies = true,
                ShowSupported = true,
                ShowBookmark = false,
                ShowSecondName = true
            };
            SettingsService.Instance.UpdateColumns(cols);

            var loaded = SettingsService.Instance.LoadSettings().Columns!;
            Assert.True(loaded.ShowId);
            Assert.True(loaded.ShowCategory);
            Assert.True(loaded.ShowTopCategory);
            Assert.True(loaded.ShowCategoryPath);
            Assert.True(loaded.ShowApplies);
            Assert.True(loaded.ShowSupported);
            Assert.False(loaded.ShowBookmark);
            Assert.True(loaded.ShowSecondName);
        }

        [Fact]
        public void ColumnLayout_RoundTrip_PreservesOrderAndVisibility()
        {
            var dir = CreateTempDir();
            SettingsService.Instance.InitializeForTests(dir);

            var layout = new[]
            {
                new ColumnState { Key = "Bookmark", Index = 0, Width = 20, Visible = true },
                new ColumnState { Key = "Name", Index = 3, Width = 300, Visible = true },
                new ColumnState { Key = "Id", Index = 5, Width = 200, Visible = true },
                new ColumnState { Key = "Category", Index = 6, Width = 150, Visible = false }
            };
            SettingsService.Instance.UpdateColumnLayout(layout.ToList());

            var round = SettingsService.Instance.LoadColumnLayout();
            Assert.Equal(layout.Length, round.Count);
            // Compare by key
            foreach (var orig in layout)
            {
                var match = round.FirstOrDefault(c => c.Key == orig.Key);
                Assert.NotNull(match);
                Assert.Equal(orig.Index, match.Index);
                Assert.Equal(orig.Width, match.Width, 3);
                Assert.Equal(orig.Visible, match.Visible);
            }
        }

        [Fact]
        public void Sort_PersistsAndClears()
        {
            var dir = CreateTempDir();
            SettingsService.Instance.InitializeForTests(dir);

            SettingsService.Instance.UpdateSort("DisplayName", "Asc");
            var s1 = SettingsService.Instance.LoadSettings();
            Assert.Equal("DisplayName", s1.SortColumn);
            Assert.Equal("Asc", s1.SortDirection);

            SettingsService.Instance.UpdateSort(null, null);
            var s2 = SettingsService.Instance.LoadSettings();
            Assert.Null(s2.SortColumn);
            Assert.Null(s2.SortDirection);
        }

        [Fact]
        public void SecondLanguage_Enable_DefaultsLanguageIfMissing()
        {
            var dir = CreateTempDir();
            SettingsService.Instance.InitializeForTests(dir);

            // ensure no prior language
            var s0 = SettingsService.Instance.LoadSettings();
            s0.SecondLanguage = null;
            SettingsService.Instance.SaveSettings(s0);

            SettingsService.Instance.UpdateSecondLanguageEnabled(true);
            var s1 = SettingsService.Instance.LoadSettings();
            Assert.True(s1.SecondLanguageEnabled);
            Assert.False(string.IsNullOrEmpty(s1.SecondLanguage));
        }
    }
}
