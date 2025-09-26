using System.Linq;
using PolicyPlusPlus.Services;
using Xunit;

namespace PolicyPlusModTests.WinUI3.PendingChanges
{
    public class PendingChangesServiceTests
        : PolicyPlusModTests.TestHelpers.PendingIsolationTestBase
    {
        [Fact]
        public void Add_New_And_Update_MergesByPolicyAndScope()
        {
            var svc = PendingChangesService.Instance;
            svc.Pending.Clear();
            svc.History.Clear();

            var change = new PendingChange
            {
                PolicyId = "MACHINE:Sample",
                PolicyName = "Sample",
                Scope = "Computer",
                Action = "Enable",
                DesiredState = PolicyState.Enabled,
                Details = "d1",
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
                Details = "d2",
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
        public void Discard_RemovesFromPending_WithoutHistory()
        {
            var svc = PendingChangesService.Instance;
            svc.Pending.Clear();
            svc.History.Clear();

            var c1 = new PendingChange
            {
                PolicyId = "ID1",
                PolicyName = "P1",
                Scope = "User",
                Action = "Enable",
                DesiredState = PolicyState.Enabled,
            };
            var c2 = new PendingChange
            {
                PolicyId = "ID2",
                PolicyName = "P2",
                Scope = "Computer",
                Action = "Disable",
                DesiredState = PolicyState.Disabled,
            };
            svc.Add(c1);
            svc.Add(c2);

            svc.Discard(c1);

            Assert.Single(svc.Pending);
            Assert.Empty(svc.History); // no history entry now
        }

        [Fact]
        public void Applied_MovesToHistoryWithApplied_And_Removes()
        {
            var svc = PendingChangesService.Instance;
            svc.Pending.Clear();
            svc.History.Clear();

            var c = new PendingChange
            {
                PolicyId = "ID3",
                PolicyName = "P3",
                Scope = "User",
                Action = "Enable",
                DesiredState = PolicyState.Enabled,
            };
            svc.Add(c);

            svc.Applied(c);

            Assert.Empty(svc.Pending);
            var applied = svc.History.Where(h => h.PolicyId == "ID3").ToList();
            Assert.Single(applied);
            Assert.Equal("Applied", applied[0].Result);
        }

        [Fact]
        public void DiscardAll_ClearsPending_WithoutHistory()
        {
            var svc = PendingChangesService.Instance;
            svc.Pending.Clear();
            svc.History.Clear();

            svc.Add(new PendingChange { PolicyId = "ID1", Scope = "User" });
            svc.Add(new PendingChange { PolicyId = "ID2", Scope = "Computer" });

            svc.DiscardAll();

            Assert.Empty(svc.Pending);
            Assert.Empty(svc.History); // no discarded history entries
        }

        [Fact]
        public void History_IsTrimmed_To100()
        {
            var svc = PendingChangesService.Instance;
            svc.Pending.Clear();
            svc.History.Clear();

            // Add 120 applied entries; expect only 100 newest remain
            for (int i = 0; i < 120; i++)
            {
                var change = new PendingChange
                {
                    PolicyId = "ID" + i,
                    PolicyName = "Name" + i,
                    Scope = "User",
                    Action = "Enable",
                    DesiredState = PolicyState.Enabled,
                    Details = "d" + i,
                };
                svc.Add(change);
                svc.Applied(change);
            }

            Assert.Equal(100, svc.History.Count);
            // The oldest kept should be ID20 (since ID0-19 removed)
            Assert.DoesNotContain(svc.History, h => h.PolicyId == "ID0");
            Assert.Contains(svc.History, h => h.PolicyId == "ID119");
        }
    }
}
