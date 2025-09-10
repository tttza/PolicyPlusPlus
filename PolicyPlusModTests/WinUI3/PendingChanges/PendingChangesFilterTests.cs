using System;
using System.Collections.Generic;
using System.Linq;
using PolicyPlusPlus.Services;
using PolicyPlusPlus.ViewModels;
using Xunit;

namespace PolicyPlusModTests.WinUI3
{
    public class PendingChangesFilterTests
    {
        private static List<PendingChange> SamplePending() => new()
        {
            new PendingChange { PolicyId = "A", PolicyName = "Alpha", Scope = "User", Action = "Enable", Details = "HKCU\\..." },
            new PendingChange { PolicyId = "B", PolicyName = "Beta", Scope = "Computer", Action = "Disable", Details = "HKLM\\..." },
            new PendingChange { PolicyId = "C", PolicyName = "Gamma", Scope = "Both", Action = "Set", Details = "HKLM\\...; HKCU\\..." },
        };

        [Fact]
        public void FilterPending_ByScope_Computer()
        {
            var result = PendingChangesFilter.FilterPending(SamplePending(), query: "", scope: "Computer", operation: "All");
            Assert.All(result, c => Assert.Equal("Computer", c.Scope));
        }

        [Fact]
        public void FilterPending_ByOperation_Disable()
        {
            var result = PendingChangesFilter.FilterPending(SamplePending(), query: "", scope: "Both", operation: "Disable");
            Assert.Single(result);
            Assert.Equal("B", result[0].PolicyId);
        }

        [Fact]
        public void FilterPending_ByQuery_MatchesNameOrDetails()
        {
            var byName = PendingChangesFilter.FilterPending(SamplePending(), query: "gam", scope: "Both", operation: "All");
            Assert.Single(byName);
            Assert.Equal("C", byName[0].PolicyId);

            var byDetails = PendingChangesFilter.FilterPending(SamplePending(), query: "HKCU", scope: "Both", operation: "All");
            Assert.Equal(2, byDetails.Count);
            Assert.Contains(byDetails, c => c.PolicyId == "A");
            Assert.Contains(byDetails, c => c.PolicyId == "C");
        }

        [Fact]
        public void BuildSummary_Text()
        {
            Assert.Equal("No pending changes", PendingChangesFilter.BuildSummary(0));
            Assert.Equal("1 pending change(s)", PendingChangesFilter.BuildSummary(1));
            Assert.Equal("2 pending change(s)", PendingChangesFilter.BuildSummary(2));
        }

        private static List<HistoryRecord> SampleHistory(DateTime baseNow)
        {
            return new List<HistoryRecord>
            {
                new HistoryRecord { PolicyId = "A", PolicyName = "Alpha", Result = "Applied", Details = "one", AppliedAt = baseNow.Date.AddHours(1) },
                new HistoryRecord { PolicyId = "B", PolicyName = "Beta", Result = "Discarded", Details = "two", AppliedAt = baseNow.Date.AddDays(-2) },
                new HistoryRecord { PolicyId = "C", PolicyName = "Gamma", Result = "Applied", Details = "three", AppliedAt = baseNow.Date.AddDays(-10) },
                new HistoryRecord { PolicyId = "D", PolicyName = "Delta", Result = "Reapplied", Details = "four", AppliedAt = baseNow.Date.AddDays(-40) },
            };
        }

        [Fact]
        public void FilterHistory_ByType()
        {
            var now = new DateTime(2024, 12, 20, 10, 0, 0);
            var src = SampleHistory(now);
            var res = PendingChangesFilter.FilterHistory(src, query: "", type: "Applied", range: "All", now: now);
            Assert.All(res, h => Assert.Equal("Applied", h.Result));
        }

        [Fact]
        public void FilterHistory_ByRange_Today()
        {
            var now = new DateTime(2024, 12, 20, 10, 0, 0);
            var src = SampleHistory(now);
            var res = PendingChangesFilter.FilterHistory(src, query: "", type: "All", range: "Today", now: now);
            Assert.All(res, h => Assert.True(h.AppliedAt >= now.Date));
        }

        [Fact]
        public void FilterHistory_ByRange_Last7Days()
        {
            var now = new DateTime(2024, 12, 20, 10, 0, 0);
            var src = SampleHistory(now);
            var res = PendingChangesFilter.FilterHistory(src, query: "", type: "All", range: "Last 7 days", now: now);
            Assert.All(res, h => Assert.True(h.AppliedAt >= now.Date.AddDays(-7)));
            Assert.DoesNotContain(res, h => h.PolicyId == "C");
        }

        [Fact]
        public void FilterHistory_Search_Query()
        {
            var now = new DateTime(2024, 12, 20, 10, 0, 0);
            var src = SampleHistory(now);
            var res = PendingChangesFilter.FilterHistory(src, query: "bet", type: "All", range: "All", now: now);
            Assert.Single(res);
            Assert.Equal("B", res[0].PolicyId);
        }
    }
}
