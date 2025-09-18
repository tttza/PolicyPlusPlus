using PolicyPlusCore.Admx;
using PolicyPlusCore.Core;
using PolicyPlusPlus.ViewModels;
using PolicyPlusPlus.Services;
using Xunit;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System;
using SharedTest;

namespace PolicyPlusModTests.WinUI3.QuickEdit
{
    public class QuickEditRowMultipleListTests
    {
        public static (QuickEditRow row, PolicyPlusPolicy policy) CreateQuickEditRow(string policyId = "DummyListPolicy", string language = "en-US")
        {
            var bundle = AdmxTestHelper.LoadBundle(out _, language);
            var pol = bundle.Policies.Values.First(p => p.RawPolicy.ID == policyId);
            var row = new QuickEditRow(pol, bundle, comp: null, user: null)
            {
                UserState = QuickEditState.Enabled,
                ComputerState = QuickEditState.Enabled
            };
            return (row, pol);
        }

        private static (QuickEditRow row, PolicyPlusPolicy pol) LoadDummyListPolicy()
            => CreateQuickEditRow("DummyListPolicy");

        [Fact]
        public void ReplaceList_ByElementId_UpdatesSecondListAndQueuesPending()
        {
            AdmxTestHelper.ClearPending();
            var (row, pol) = LoadDummyListPolicy();

            Assert.True(row.OptionElements.Count(e => e.Type == OptionElementType.List) >= 2);
            var simple = row.OptionElements.First(e => e.Type == OptionElementType.List && !e.ProvidesNames && e.Id == "ListValues");
            var named = row.OptionElements.First(e => e.Type == OptionElementType.List && e.ProvidesNames && e.Id == "NamedList");

            var simpleItems = new List<string> { "A", "B", "C" };
            var namedItems = new List<KeyValuePair<string, string>>
            {
                new("Key1","Val1"),
                new("Key2","Val2")
            };
            bool r1 = row.ReplaceList("ListValues", true, simpleItems);
            bool r2 = row.ReplaceNamedList("NamedList", true, namedItems);

            Assert.True(r1);
            Assert.True(r2);
            Assert.Equal(simpleItems, simple.UserListItems);
            Assert.Equal(namedItems, named.UserNamedListItems);

            var pending = PendingChangesService.Instance.Pending.FirstOrDefault(p => p.PolicyId == pol.UniqueID && p.Scope == "User");
            Assert.NotNull(pending);
            Assert.NotNull(pending!.Options);
            Assert.Contains("ListValues", pending.Options.Keys);
            Assert.Contains("NamedList", pending.Options.Keys);
        }

        [Fact]
        public void ReplaceMultiText_ByElementId_ReturnsFalseWhenAbsent()
        {
            AdmxTestHelper.ClearPending();
            var (row, _) = LoadDummyListPolicy();
            var ok = row.ReplaceMultiText("NonExistingMulti", true, new List<string> { "x" });
            Assert.False(ok);
        }
    }
}
