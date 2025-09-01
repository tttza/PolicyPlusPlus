using System;
using System.Collections.Generic;
using System.Linq;
using PolicyPlus.WinUI3.Services;
using PolicyPlus;
using Xunit;

namespace PolicyPlusModTests.WinUI3
{
    public class PendingChangesServiceTests
    {
        [Fact]
        public void Add_New_And_Update_MergesByPolicyAndScope()
        {
            var svc = PendingChangesService.Instance;
            svc.Pending.Clear(); svc.History.Clear();

            var change = new PendingChange
            {
                PolicyId = "MACHINE:Sample",
                PolicyName = "Sample",
                Scope = "Computer",
                Action = "Enable",
                DesiredState = PolicyState.Enabled,
                Details = "d1"
            };
            svc.Add(change);
            Assert.Single(svc.Pending);

            var updated = new PendingChange
            {
                PolicyId = "MACHINE:Sample",
                PolicyName = "Sample2",
                Scope = "Computer",
                Action = "Disable",
                DesiredState = PolicyState.Disabled,
                Details = "d2"
            };
            svc.Add(updated);
            Assert.Single(svc.Pending);
            var p = svc.Pending.First();
            Assert.Equal("Disable", p.Action);
            Assert.Equal(PolicyState.Disabled, p.DesiredState);
            Assert.Equal("Sample2", p.PolicyName);
            Assert.Equal("d2", p.Details);
        }

        [Fact]
        public void Discard_MovesToHistory_And_RemovesFromPending()
        {
            var svc = PendingChangesService.Instance;
            svc.Pending.Clear(); svc.History.Clear();

            var c1 = new PendingChange { PolicyId = "ID1", PolicyName = "P1", Scope = "User", Action = "Enable", DesiredState = PolicyState.Enabled };
            var c2 = new PendingChange { PolicyId = "ID2", PolicyName = "P2", Scope = "Computer", Action = "Disable", DesiredState = PolicyState.Disabled };
            svc.Add(c1); svc.Add(c2);

            svc.Discard(c1);

            Assert.Single(svc.Pending);
            // One history entry added for discarded item
            var discarded = svc.History.Where(h => h.PolicyId == "ID1").ToList();
            Assert.Single(discarded);
            Assert.Equal("Discarded", discarded[0].Result);
        }

        [Fact]
        public void Applied_MovesToHistoryWithApplied_And_Removes()
        {
            var svc = PendingChangesService.Instance;
            svc.Pending.Clear(); svc.History.Clear();

            var c = new PendingChange { PolicyId = "ID3", PolicyName = "P3", Scope = "User", Action = "Enable", DesiredState = PolicyState.Enabled };
            svc.Add(c);

            svc.Applied(c);

            Assert.Empty(svc.Pending);
            var applied = svc.History.Where(h => h.PolicyId == "ID3").ToList();
            Assert.Single(applied);
            Assert.Equal("Applied", applied[0].Result);
        }

        [Fact]
        public void DiscardAll_ClearsPending_And_AddsHistory()
        {
            var svc = PendingChangesService.Instance;
            svc.Pending.Clear(); svc.History.Clear();

            svc.Add(new PendingChange { PolicyId = "ID1", Scope = "User" });
            svc.Add(new PendingChange { PolicyId = "ID2", Scope = "Computer" });

            svc.DiscardAll();

            Assert.Empty(svc.Pending);
            var ids = svc.History.Select(h => h.PolicyId).OrderBy(x => x).ToArray();
            Assert.Equal(new[] { "ID1", "ID2" }, ids);
            Assert.All(svc.History, h => Assert.Equal("Discarded", h.Result));
        }
    }
}
