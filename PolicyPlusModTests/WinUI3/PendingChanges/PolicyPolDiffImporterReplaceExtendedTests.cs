using System;
using System.Collections.Generic;
using System.Linq;
using PolicyPlusCore.Admx;
using PolicyPlusCore.Core;
using PolicyPlusCore.IO;
using PolicyPlusModTests.Testing;
using PolicyPlusPlus.Services;
using Xunit;

namespace PolicyPlusModTests.WinUI3.PendingChanges
{
    public class PolicyPolDiffImporterReplaceExtendedTests
    {
        private sealed class InMemoryPolicySource : IPolicySource
        {
            private readonly PolFile _pol;

            public InMemoryPolicySource(PolFile pol)
            {
                _pol = pol;
            }

            public bool ContainsValue(string Key, string Value) => _pol.ContainsValue(Key, Value);

            public object? GetValue(string Key, string Value) => _pol.GetValue(Key, Value);

            public bool WillDeleteValue(string Key, string Value) =>
                _pol.WillDeleteValue(Key, Value);

            public List<string> GetValueNames(string Key) => _pol.GetValueNames(Key);

            public void SetValue(
                string Key,
                string Value,
                object Data,
                Microsoft.Win32.RegistryValueKind DataType
            ) => _pol.SetValue(Key, Value, Data, DataType);

            public void ForgetValue(string Key, string Value) => _pol.ForgetValue(Key, Value);

            public void DeleteValue(string Key, string Value) => _pol.DeleteValue(Key, Value);

            public void ClearKey(string Key) => _pol.ClearKey(Key);

            public void ForgetKeyClearance(string Key) => _pol.ForgetKeyClearance(Key);
        }

        private static AdmxBundle Bundle(params PolicyPlusPolicy[] policies)
        {
            var b = new AdmxBundle { Policies = new Dictionary<string, PolicyPlusPolicy>() };
            foreach (var p in policies)
                b.Policies[p.UniqueID] = p;
            return b;
        }

        private void SetSources(PolFile comp, PolFile user)
        {
            var mgr = (PolicySourceManager)PolicySourceManager.Instance;
            lock (PolicySourceManager.SourcesSync)
            {
                mgr.CompSource = new InMemoryPolicySource(comp);
                mgr.UserSource = new InMemoryPolicySource(user);
            }
        }

        private static RegFile EmptyReg()
        {
            var r = new RegFile();
            r.SetPrefix(string.Empty);
            return r;
        }

        public PolicyPolDiffImporterReplaceExtendedTests()
        {
            PendingChangesService.EnableTestIsolation();
            PendingChangesService.ResetAmbientForTest();
            PolicyPlusCore.Core.ConfiguredPolicyTracker.Reset();
        }

        [Fact(DisplayName = "Replace: Empty .reg queues Clear for all configured policies")]
        public void Replace_EmptyReg_AllClear()
        {
            var toggle = TestPolicyFactory.CreateSimpleTogglePolicy(
                "MACHINE:ToggleA",
                regKey: "Software\\RepTestA",
                regValue: "ValA"
            );
            var list = TestPolicyFactory.CreateListPolicy("MACHINE:ListX");
            var enumPol = TestPolicyFactory.CreateEnumPolicy("USER:EnumY");
            enumPol.RawPolicy.Section = AdmxPolicySection.User;
            var bundle = Bundle(toggle, list, enumPol);
            var compPol = new PolFile();
            var userPol = new PolFile();
            PolicyProcessing.SetPolicyState(
                compPol,
                toggle,
                PolicyState.Enabled,
                new Dictionary<string, object>()
            );
            PolicyProcessing.SetPolicyState(
                compPol,
                list,
                PolicyState.Enabled,
                new Dictionary<string, object>
                {
                    {
                        "ListElem",
                        new List<string> { "Item1", "Item2" }
                    },
                }
            );
            PolicyProcessing.SetPolicyState(
                userPol,
                enumPol,
                PolicyState.Enabled,
                new Dictionary<string, object> { { "EnumElem", 0 } }
            );
            SetSources(compPol, userPol);
            PendingChangesService.Instance.DiscardAll();
            int queued = RegImportQueueHelper.Queue(EmptyReg(), bundle, replace: true);
            // 3 Clear actions expected
            Assert.Equal(3, queued);
            Assert.All(
                PendingChangesService.Instance.Pending,
                p => Assert.Equal("Clear", p.Action)
            );
            Assert.Equal(
                new[] { toggle.UniqueID, list.UniqueID, enumPol.UniqueID }.OrderBy(x => x),
                PendingChangesService.Instance.Pending.Select(p => p.PolicyId).OrderBy(x => x)
            );
        }

        [Fact(
            DisplayName = "Replace: Disabled policies absent in reg become Clear (not left Disabled)"
        )]
        public void Replace_DisabledToClear()
        {
            var toggle = TestPolicyFactory.CreateSimpleTogglePolicy("MACHINE:ToggleD");
            var bundle = Bundle(toggle);
            var compPol = new PolFile();
            PolicyProcessing.SetPolicyState(
                compPol,
                toggle,
                PolicyState.Disabled,
                new Dictionary<string, object>()
            );
            SetSources(compPol, new PolFile());
            PendingChangesService.Instance.DiscardAll();
            int queued = RegImportQueueHelper.Queue(EmptyReg(), bundle, replace: true);
            Assert.Equal(1, queued);
            var pc = Assert.Single(PendingChangesService.Instance.Pending);
            Assert.Equal("Clear", pc.Action);
        }

        [Fact(DisplayName = "Replace: Mixed change/keep/add/clear handled correctly")]
        public void Replace_MixedOperations()
        {
            // A same, B change, C disabled->clear, D NotConfigured, E new
            var A = TestPolicyFactory.CreateSimpleTogglePolicy(
                "MACHINE:A",
                regKey: "Software\\MixA",
                regValue: "Val"
            );
            var B = TestPolicyFactory.CreateSimpleTogglePolicy(
                "MACHINE:B",
                regKey: "Software\\MixB",
                regValue: "Val"
            );
            var C = TestPolicyFactory.CreateSimpleTogglePolicy(
                "MACHINE:C",
                regKey: "Software\\MixC",
                regValue: "Val"
            );
            var D = TestPolicyFactory.CreateSimpleTogglePolicy(
                "MACHINE:D",
                regKey: "Software\\MixD",
                regValue: "Val"
            );
            var E = TestPolicyFactory.CreateSimpleTogglePolicy(
                "MACHINE:E",
                regKey: "Software\\MixE",
                regValue: "Val"
            );
            var bundle = Bundle(A, B, C, D, E);
            var compPol = new PolFile();
            PolicyProcessing.SetPolicyState(
                compPol,
                A,
                PolicyState.Enabled,
                new Dictionary<string, object>()
            );
            PolicyProcessing.SetPolicyState(
                compPol,
                B,
                PolicyState.Enabled,
                new Dictionary<string, object>()
            );
            PolicyProcessing.SetPolicyState(
                compPol,
                C,
                PolicyState.Disabled,
                new Dictionary<string, object>()
            );
            // D NotConfigured
            SetSources(compPol, new PolFile());
            // .reg: A enabled (same), B disabled (change), E enabled (new)
            var reg = new RegFile();
            reg.SetPrefix(string.Empty);
            RegFile.RegFileKey keyA = new() { Name = "HKEY_LOCAL_MACHINE\\Software\\MixA" };
            keyA.Values.Add(
                new RegFile.RegFileValue
                {
                    Name = "Val",
                    Data = 1U,
                    Kind = Microsoft.Win32.RegistryValueKind.DWord,
                }
            );
            RegFile.RegFileKey keyB = new() { Name = "HKEY_LOCAL_MACHINE\\Software\\MixB" };
            keyB.Values.Add(
                new RegFile.RegFileValue
                {
                    Name = "Val",
                    Data = 0U,
                    Kind = Microsoft.Win32.RegistryValueKind.DWord,
                }
            ); // disable
            RegFile.RegFileKey keyE = new() { Name = "HKEY_LOCAL_MACHINE\\Software\\MixE" };
            keyE.Values.Add(
                new RegFile.RegFileValue
                {
                    Name = "Val",
                    Data = 1U,
                    Kind = Microsoft.Win32.RegistryValueKind.DWord,
                }
            );
            reg.Keys.Add(keyA);
            reg.Keys.Add(keyB);
            reg.Keys.Add(keyE);
            PendingChangesService.Instance.DiscardAll();
            int queued = RegImportQueueHelper.Queue(reg, bundle, replace: true);
            // Expect B change (Disable), C clear, E enable. A same -> skip. D absent + NotConfigured -> skip.
            Assert.Equal(3, queued);
            var actions = PendingChangesService.Instance.Pending.ToDictionary(
                p => p.PolicyId,
                p => p.Action
            );
            Assert.Equal("Disable", actions[B.UniqueID]);
            Assert.Equal("Clear", actions[C.UniqueID]);
            Assert.Equal("Enable", actions[E.UniqueID]);
            Assert.DoesNotContain(A.UniqueID, actions.Keys);
            Assert.DoesNotContain(D.UniqueID, actions.Keys);
        }

        [Fact(
            DisplayName = "Replace: Named list identical but different key order -> no diff (skip)"
        )]
        public void Replace_NamedList_OrderStable()
        {
            var named = TestPolicyFactory.CreateNamedListPolicy("MACHINE:NamedOrder");
            var bundle = Bundle(named);
            var compPol = new PolFile();
            // Simulate entries: Key1=Alpha, Key2=Beta
            compPol.SetValue(
                named.RawPolicy.RegistryKey,
                "Key1",
                "Alpha",
                Microsoft.Win32.RegistryValueKind.String
            );
            compPol.SetValue(
                named.RawPolicy.RegistryKey,
                "Key2",
                "Beta",
                Microsoft.Win32.RegistryValueKind.String
            );
            PolicyProcessing.SetPolicyState(
                compPol,
                named,
                PolicyState.Enabled,
                new Dictionary<string, object>
                {
                    {
                        "ListElem",
                        new Dictionary<string, string> { { "Key1", "Alpha" }, { "Key2", "Beta" } }
                    },
                }
            );
            SetSources(compPol, new PolFile());
            // .reg with reversed declaration order
            var reg = new RegFile();
            reg.SetPrefix(string.Empty);
            var key = new RegFile.RegFileKey
            {
                Name = "HKEY_LOCAL_MACHINE\\" + named.RawPolicy.RegistryKey,
            };
            key.Values.Add(
                new RegFile.RegFileValue
                {
                    Name = "Key2",
                    Data = "Beta",
                    Kind = Microsoft.Win32.RegistryValueKind.String,
                }
            );
            key.Values.Add(
                new RegFile.RegFileValue
                {
                    Name = "Key1",
                    Data = "Alpha",
                    Kind = Microsoft.Win32.RegistryValueKind.String,
                }
            );
            reg.Keys.Add(key);
            PendingChangesService.Instance.DiscardAll();
            int queued = RegImportQueueHelper.Queue(reg, bundle, replace: true);
            // Should only result in Clear if absent; but it's present & identical -> no diff.
            Assert.Equal(0, queued);
            Assert.Empty(PendingChangesService.Instance.Pending);
        }

        [Fact(
            DisplayName = "Replace: MultiText identical ordering -> skip; reordered -> diff (order significant)"
        )]
        public void Replace_MultiText_OrderStable()
        {
            var mt = TestPolicyFactory.CreateMultiTextPolicy("MACHINE:MTStable");
            var bundle = Bundle(mt);
            var compPol = new PolFile();
            PolicyProcessing.SetPolicyState(
                compPol,
                mt,
                PolicyState.Enabled,
                new Dictionary<string, object>
                {
                    { "MultiTextElem", new string[] { "Line1", "Line2", "Line3" } },
                }
            );
            SetSources(compPol, new PolFile());
            // .reg replicates same array (multiText stored as REG_MULTI_SZ -> represented as string[]). We'll simulate using the same ordering.
            var reg = new RegFile();
            reg.SetPrefix(string.Empty);
            var key = new RegFile.RegFileKey
            {
                Name = "HKEY_LOCAL_MACHINE\\" + mt.RawPolicy.RegistryKey,
            };
            key.Values.Add(
                new RegFile.RegFileValue
                {
                    Name = mt.RawPolicy.RegistryValue,
                    Data = new string[] { "Line1", "Line2", "Line3" },
                    Kind = Microsoft.Win32.RegistryValueKind.MultiString,
                }
            );
            reg.Keys.Add(key);
            PendingChangesService.Instance.DiscardAll();
            int queuedSame = RegImportQueueHelper.Queue(reg, bundle, replace: true);
            Assert.Equal(0, queuedSame);
            // Reordered version: ordering is treated as significant for multi-text (e.g. some policies interpret line order). Therefore a reorder should queue a change.
            var regReorder = new RegFile();
            regReorder.SetPrefix(string.Empty);
            var key2 = new RegFile.RegFileKey
            {
                Name = "HKEY_LOCAL_MACHINE\\" + mt.RawPolicy.RegistryKey,
            };
            key2.Values.Add(
                new RegFile.RegFileValue
                {
                    Name = mt.RawPolicy.RegistryValue,
                    Data = new string[] { "Line2", "Line1", "Line3" },
                    Kind = Microsoft.Win32.RegistryValueKind.MultiString,
                }
            );
            regReorder.Keys.Add(key2);
            PendingChangesService.Instance.DiscardAll();
            int queuedReorder = RegImportQueueHelper.Queue(regReorder, bundle, replace: true);
            // Order matters: reordering should produce a diff (at least 1 change queued)
            Assert.True(queuedReorder >= 1);
        }

        [Fact(DisplayName = "Replace: Toggle disabled in reg remains Disable (not Clear)")]
        public void Replace_DisabledSpecified_NotClear()
        {
            var toggle = TestPolicyFactory.CreateSimpleTogglePolicy("MACHINE:ToggleDis");
            var bundle = Bundle(toggle);
            var compPol = new PolFile();
            // Currently enabled
            PolicyProcessing.SetPolicyState(
                compPol,
                toggle,
                PolicyState.Enabled,
                new Dictionary<string, object>()
            );
            SetSources(compPol, new PolFile());
            // .reg sets it disabled (value 0)
            var reg = new RegFile();
            reg.SetPrefix(string.Empty);
            var k = new RegFile.RegFileKey
            {
                Name = "HKEY_LOCAL_MACHINE\\" + toggle.RawPolicy.RegistryKey,
            };
            k.Values.Add(
                new RegFile.RegFileValue
                {
                    Name = toggle.RawPolicy.RegistryValue,
                    Data = 0U,
                    Kind = Microsoft.Win32.RegistryValueKind.DWord,
                }
            );
            reg.Keys.Add(k);
            PendingChangesService.Instance.DiscardAll();
            int queued = RegImportQueueHelper.Queue(reg, bundle, replace: true);
            Assert.Equal(1, queued);
            var pc = Assert.Single(PendingChangesService.Instance.Pending);
            Assert.Equal("Disable", pc.Action);
        }

        [Fact(DisplayName = "Replace: Trim differences in text ignored (no diff)")]
        public void Replace_TrimmedText_NoDiff()
        {
            var text = TestPolicyFactory.CreateTextPolicy("MACHINE:TextTrim");
            var bundle = Bundle(text);
            var compPol = new PolFile();
            PolicyProcessing.SetPolicyState(
                compPol,
                text,
                PolicyState.Enabled,
                new Dictionary<string, object> { { "TextElem", "Value" } }
            );
            SetSources(compPol, new PolFile());
            // .reg with trailing spaces
            var reg = new RegFile();
            reg.SetPrefix(string.Empty);
            var k = new RegFile.RegFileKey
            {
                Name = "HKEY_LOCAL_MACHINE\\" + text.RawPolicy.RegistryKey,
            };
            k.Values.Add(
                new RegFile.RegFileValue
                {
                    Name = text.RawPolicy.RegistryValue,
                    Data = "Value  ",
                    Kind = Microsoft.Win32.RegistryValueKind.String,
                }
            );
            reg.Keys.Add(k);
            PendingChangesService.Instance.DiscardAll();
            int queued = RegImportQueueHelper.Queue(reg, bundle, replace: true);
            Assert.Equal(0, queued);
        }

        [Fact(
            DisplayName = "Heuristic: OnValue/OffValue defined -> numeric 0/1 not remapped (no implicit Disable)"
        )]
        public void Toggle_OnOffValues_NoHeuristicApplied()
        {
            // Create a toggle with explicit OnValue (numeric 5) so heuristic (which requires both null) must not run.
            var toggle = TestPolicyFactory.CreateSimpleTogglePolicy("MACHINE:ToggleHeur");
            toggle.RawPolicy.AffectedValues.OnValue = new PolicyRegistryValue
            {
                NumberValue = 5U,
                RegistryType = PolicyRegistryValueType.Numeric,
            };
            var bundle = Bundle(toggle);
            var compPol = new PolFile();
            // Current state Enabled to observe potential incorrect Disable mapping.
            PolicyProcessing.SetPolicyState(
                compPol,
                toggle,
                PolicyState.Enabled,
                new Dictionary<string, object>()
            );
            SetSources(compPol, new PolFile());
            // .reg provides numeric 0 which does NOT match OnValue=5 and OffValue is null (deletion would represent Disabled). Should evaluate as NotConfigured and queue Clear (replace mode) NOT Disable.
            var reg = new RegFile();
            reg.SetPrefix(string.Empty);
            var key = new RegFile.RegFileKey
            {
                Name = "HKEY_LOCAL_MACHINE\\" + toggle.RawPolicy.RegistryKey,
            };
            key.Values.Add(
                new RegFile.RegFileValue
                {
                    Name = toggle.RawPolicy.RegistryValue,
                    Data = 0U,
                    Kind = Microsoft.Win32.RegistryValueKind.DWord,
                }
            );
            reg.Keys.Add(key);
            PendingChangesService.Instance.DiscardAll();
            int queued = RegImportQueueHelper.Queue(reg, bundle, replace: true);
            Assert.Equal(1, queued);
            var pc = Assert.Single(PendingChangesService.Instance.Pending);
            Assert.Equal("Clear", pc.Action); // heuristic should not flip to Disable
        }

        [Fact(
            DisplayName = "Replace: List missing first index yields empty list (gap stops enumeration)"
        )]
        public void Replace_List_MissingEntry_GapStopsEnumeration_QueuesChange()
        {
            var list = TestPolicyFactory.CreateListPolicy("MACHINE:ListMissing");
            var bundle = Bundle(list);
            var compPol = new PolFile();
            PolicyProcessing.SetPolicyState(
                compPol,
                list,
                PolicyState.Enabled,
                new Dictionary<string, object>
                {
                    {
                        "ListElem",
                        new List<string> { "One", "Two", "Three" }
                    },
                }
            );
            SetSources(compPol, new PolFile());
            // .reg with only Two, Three
            var reg = new RegFile();
            reg.SetPrefix(string.Empty);
            var key = new RegFile.RegFileKey
            {
                Name = "HKEY_LOCAL_MACHINE\\" + list.RawPolicy.RegistryKey,
            };
            // Simulate prefix entries (ListPrefix1=One, ListPrefix2=Two etc). Only keep last two.
            key.Values.Add(
                new RegFile.RegFileValue
                {
                    Name = list.RawPolicy.RegistryValue + "2",
                    Data = "Two",
                    Kind = Microsoft.Win32.RegistryValueKind.String,
                }
            );
            key.Values.Add(
                new RegFile.RegFileValue
                {
                    Name = list.RawPolicy.RegistryValue + "3",
                    Data = "Three",
                    Kind = Microsoft.Win32.RegistryValueKind.String,
                }
            );
            reg.Keys.Add(key);
            PendingChangesService.Instance.DiscardAll();
            int queued = RegImportQueueHelper.Queue(reg, bundle, replace: true);
            Assert.Equal(1, queued);
            var pc = Assert.Single(PendingChangesService.Instance.Pending);
            Assert.Equal("Enable", pc.Action); // change of list contents still represented as Enable
            Assert.NotNull(pc.Options);
            var opts = pc.Options!;
            Assert.True(opts.ContainsKey("ListElem"));
            var arr = Assert.IsType<List<string>>(opts["ListElem"]);
            // Spec-contiguous enumeration stops at first missing numeric suffix, so only suffixes beginning at 1 are considered.
            // Because suffix 1 ("One") is absent, enumeration yields an empty list.
            Assert.Empty(arr);
        }

        [Fact(DisplayName = "Replace: Enum unchanged -> no diff queued")]
        public void Replace_Enum_Unchanged_NoDiff()
        {
            var enumPol = TestPolicyFactory.CreateEnumPolicy("MACHINE:EnumNoDiff");
            var bundle = Bundle(enumPol);
            var compPol = new PolFile();
            // Set current to value 2 (index based numeric value in Items[1])
            PolicyProcessing.SetPolicyState(
                compPol,
                enumPol,
                PolicyState.Enabled,
                new Dictionary<string, object> { { "EnumElem", 1 } }
            );
            SetSources(compPol, new PolFile());
            // .reg replicates same numeric value 2
            var reg = new RegFile();
            reg.SetPrefix(string.Empty);
            var key = new RegFile.RegFileKey
            {
                Name = "HKEY_LOCAL_MACHINE\\" + enumPol.RawPolicy.RegistryKey,
            };
            key.Values.Add(
                new RegFile.RegFileValue
                {
                    Name = enumPol.RawPolicy.RegistryValue,
                    Data = 2U,
                    Kind = Microsoft.Win32.RegistryValueKind.DWord,
                }
            );
            reg.Keys.Add(key);
            PendingChangesService.Instance.DiscardAll();
            int queued = RegImportQueueHelper.Queue(reg, bundle, replace: true);
            Assert.Equal(0, queued);
            Assert.Empty(PendingChangesService.Instance.Pending);
        }
    }
}
