using System;
using System.Collections.Generic;
using System.Linq;
using PolicyPlus.Core.Core;
using PolicyPlus.WinUI3.Services;
using PolicyPlusModTests.TestHelpers;
using Xunit;

namespace PolicyPlusModTests.WinUI3
{
    // Tests for ensuring that options recorded in HistoryRecord are correctly reapplied.
    public class ReapplyHistoryOptionsTests
    {
        private static (PolicyPlusPolicy policy, Dictionary<string, object> opts) BuildTextCase()
        {
            var pol = TestPolicyFactory.CreateTextPolicy("MACHINE:ReapplyText");
            var opts = new Dictionary<string, object> { { "TextElem", "ReapplyString" } };
            return (pol, opts);
        }

        private static (PolicyPlusPolicy policy, Dictionary<string, object> opts) BuildEnumCase()
        {
            var pol = TestPolicyFactory.CreateEnumPolicy("MACHINE:ReapplyEnum");
            var opts = new Dictionary<string, object> { { "EnumElem", 1 } }; // second item (numeric value 2)
            return (pol, opts);
        }

        private static (PolicyPlusPolicy policy, Dictionary<string, object> opts) BuildListCase()
        {
            var pol = TestPolicyFactory.CreateListPolicy("MACHINE:ReapplyList");
            var opts = new Dictionary<string, object> { { "ListElem", new List<string>{ "L1", "L2" } } };
            return (pol, opts);
        }

        private static (PolicyPlusPolicy policy, Dictionary<string, object> opts) BuildMultiTextCase()
        {
            var pol = TestPolicyFactory.CreateMultiTextPolicy("MACHINE:ReapplyMultiText");
            var opts = new Dictionary<string, object> { { "MultiTextElem", new List<string>{ "M1", "M2" } } };
            return (pol, opts);
        }

        private static (PolicyPlusPolicy policy, Dictionary<string, object> opts) BuildDecimalCase()
        {
            var pol = TestPolicyFactory.CreateDecimalPolicy("MACHINE:ReapplyDecimal");
            // choose a mid-range value within min/max (factory sets min=0 max=500 default=100)
            var opts = new Dictionary<string, object> { { "DecimalElem", 250u } };
            return (pol, opts);
        }

        private static void ApplyAndRecord(PolicyPlusPolicy policy, Dictionary<string, object> opts)
        {
            // Simulate saving: create pending change then Applied() to push to history
            var change = new PendingChange
            {
                PolicyId = policy.UniqueID,
                PolicyName = policy.DisplayName,
                Scope = "Computer",
                Action = "Enable",
                DesiredState = PolicyState.Enabled,
                Options = opts,
                Details = "test",
                DetailsFull = "test full"
            };
            PendingChangesService.Instance.Pending.Clear();
            PendingChangesService.Instance.History.Clear();
            PendingChangesService.Instance.Add(change);
            PendingChangesService.Instance.Applied(change);
        }

        [Theory(DisplayName = "History stores option types intact for reapply" )]
        [InlineData("Text")]
        [InlineData("Enum")]
        [InlineData("List")]
        [InlineData("MultiText")]
        [InlineData("Decimal")]
        public void History_StoresOptions_Intact(string kind)
        {
            PolicyPlusPolicy pol; Dictionary<string, object> opts;
            switch(kind)
            {
                case "Text": (pol, opts) = BuildTextCase(); break;
                case "Enum": (pol, opts) = BuildEnumCase(); break;
                case "List": (pol, opts) = BuildListCase(); break;
                case "MultiText": (pol, opts) = BuildMultiTextCase(); break;
                case "Decimal": (pol, opts) = BuildDecimalCase(); break;
                default: throw new InvalidOperationException();
            }
            ApplyAndRecord(pol, opts);
            var h = PendingChangesService.Instance.History.FirstOrDefault(x => x.PolicyId == pol.UniqueID);
            Assert.NotNull(h);
            Assert.Equal(PolicyState.Enabled, h!.DesiredState);
            foreach(var kv in opts)
            {
                Assert.True(h.Options!.ContainsKey(kv.Key));
                if (kv.Value is List<string> list)
                {
                    var stored = h.Options[kv.Key];
                    var list2 = (stored as IEnumerable<string>)?.ToList();
                    Assert.NotNull(list2);
                    Assert.True(list.SequenceEqual(list2!));
                }
                else if (kv.Value is string s)
                {
                    Assert.Equal(s, h.Options[kv.Key] as string);
                }
                else if (kv.Value is int i)
                {
                    Assert.Equal(i, (int)h.Options[kv.Key]);
                }
                else if (kv.Value is uint u)
                {
                    Assert.Equal(u, (uint)h.Options[kv.Key]);
                }
            }
        }
    }
}
