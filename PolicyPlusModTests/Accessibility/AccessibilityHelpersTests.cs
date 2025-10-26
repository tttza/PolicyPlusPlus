using PolicyPlusPlus.Converters;
using PolicyPlusPlus.Models;
using Xunit;

namespace PolicyPlusModTests.Accessibility
{
    // Focused converter tests (UI elements omitted for stability).
    public class AccessibilityHelpersTests
    {
        [Fact]
        public void CategoryIcon_True_ReturnsFolder()
        {
            var conv = new PolicyIconAccessibleNameConverter();
            var name = conv.Convert(true, typeof(object), "CategoryIcon", "en-US");
            Assert.Equal("Category folder", name);
        }

        [Fact]
        public void CategoryIcon_False_ReturnsNone()
        {
            var conv = new PolicyIconAccessibleNameConverter();
            var name = conv.Convert(false, typeof(object), "CategoryIcon", "en-US");
            Assert.Equal("Category: None", name);
        }

        [Fact]
        public void Bookmark_True_ReturnsBookmarked()
        {
            var conv = new PolicyIconAccessibleNameConverter();
            var name = conv.Convert(true, typeof(object), "Bookmark", "en-US");
            Assert.Equal("Bookmark: Bookmarked", name);
        }

        [Fact]
        public void Bookmark_False_ReturnsNotBookmarked()
        {
            var conv = new PolicyIconAccessibleNameConverter();
            var name = conv.Convert(false, typeof(object), "Bookmark", "en-US");
            Assert.Equal("Bookmark: Not bookmarked", name);
        }

        [Theory]
        [InlineData("Enabled", false, "User policy: Enabled")]
        [InlineData("Disabled", true, "User policy: Disabled (Pending)")]
        [InlineData("Not configured", false, "User policy: Not configured")]
        public void UserState_Row_PendingAware(string state, bool pending, string expected)
        {
            var row = PolicyListRow.FromCategory(
                new PolicyPlusCore.Core.PolicyPlusCategory { DisplayName = "Test" }
            );
            // simulate fields
            typeof(PolicyListRow).GetProperty("UserStateText")!.SetValue(row, state);
            typeof(PolicyListRow).GetProperty("UserPending")!.SetValue(row, pending);
            var conv = new PolicyIconAccessibleNameConverter();
            var name = conv.Convert(row, typeof(object), "UserState", "en-US");
            Assert.Equal(expected, name);
        }

        [Theory]
        [InlineData("Enabled", true, "Computer policy: Enabled (Pending)")]
        [InlineData("Disabled", false, "Computer policy: Disabled")]
        public void ComputerState_Row_PendingAware(string state, bool pending, string expected)
        {
            var row = PolicyListRow.FromCategory(
                new PolicyPlusCore.Core.PolicyPlusCategory { DisplayName = "Test" }
            );
            typeof(PolicyListRow).GetProperty("ComputerStateText")!.SetValue(row, state);
            typeof(PolicyListRow).GetProperty("ComputerPending")!.SetValue(row, pending);
            var conv = new PolicyIconAccessibleNameConverter();
            var name = conv.Convert(row, typeof(object), "ComputerState", "en-US");
            Assert.Equal(expected, name);
        }
    }
}
