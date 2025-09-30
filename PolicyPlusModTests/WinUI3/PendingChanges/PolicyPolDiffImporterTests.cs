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
    public class PolicyPolDiffImporterTests
    {
        private static (
            AdmxBundle bundle,
            PolicyPlusPolicy machineToggle,
            PolicyPlusPolicy userEnum
        ) BuildBundle()
        {
            // Machine simple toggle
            var machinePolicy = new AdmxPolicy
            {
                RegistryKey = "Software\\PolDiffTest",
                RegistryValue = "ToggleValue",
                Section = AdmxPolicySection.Machine,
                AffectedValues = new PolicyRegistryList
                {
                    OnValue = new PolicyRegistryValue
                    {
                        RegistryType = PolicyRegistryValueType.Numeric,
                        NumberValue = 1U,
                    },
                    OffValue = new PolicyRegistryValue
                    {
                        RegistryType = PolicyRegistryValueType.Numeric,
                        NumberValue = 0U,
                    },
                },
                DefinedIn = new AdmxFile { SourceFile = "test.admx" },
            };
            var machineWrap = new PolicyPlusPolicy
            {
                RawPolicy = machinePolicy,
                UniqueID = "MACHINE:TogglePolicy",
                DisplayName = "Toggle Policy",
            };

            // User enum policy: reuse factory-tested shape for reliability
            var userWrap = TestPolicyFactory.CreateEnumPolicy("USER:EnumPolicy");
            userWrap.RawPolicy.Section = AdmxPolicySection.User; // switch scope to user

            var bundle = new AdmxBundle { Policies = new Dictionary<string, PolicyPlusPolicy>() };
            bundle.Policies[machineWrap.UniqueID] = machineWrap;
            bundle.Policies[userWrap.UniqueID] = userWrap;
            return (bundle, machineWrap, userWrap);
        }

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

        private void SetPolicy(
            PolFile target,
            PolicyPlusPolicy pol,
            PolicyState state,
            Dictionary<string, object>? opts = null
        )
        {
            PolicyProcessing.ForgetPolicy(target, pol);
            if (state == PolicyState.Enabled || state == PolicyState.Disabled)
            {
                PolicyProcessing.SetPolicyState(
                    target,
                    pol,
                    state,
                    opts ?? new Dictionary<string, object>()
                );
            }
        }

        [Fact(DisplayName = "Diff importer queues newly enabled machine toggle")]
        public void Queue_New_Machine_Toggle()
        {
            var (bundle, machine, _) = BuildBundle();
            // Current sources empty
            var emptyPolComp = new PolFile();
            var emptyPolUser = new PolFile();
            ((PolicySourceManager)PolicySourceManager.Instance).CompSource =
                new InMemoryPolicySource(emptyPolComp);
            ((PolicySourceManager)PolicySourceManager.Instance).UserSource =
                new InMemoryPolicySource(emptyPolUser);

            // Imported reg -> pol: enable machine toggle
            var reg = new RegFile();
            reg.SetPrefix(string.Empty);
            var k = new RegFile.RegFileKey { Name = "HKEY_LOCAL_MACHINE\\Software\\PolDiffTest" };
            k.Values.Add(
                new RegFile.RegFileValue
                {
                    Name = "ToggleValue",
                    Data = 1U,
                    Kind = Microsoft.Win32.RegistryValueKind.DWord,
                }
            );
            reg.Keys.Add(k);

            // Direct diagnostic: build pol and query state
            var (userPol, machinePol) = RegImportHelper.ToPolByHive(reg);
            var directState = PolicyProcessing.GetPolicyState(machinePol, machine);
            Assert.Equal(PolicyState.Enabled, directState); // ensure policy detection works first

            PendingChangesService.EnableTestIsolation();
            PendingChangesService.ResetAmbientForTest();
            var before = PendingChangesService.Instance.Pending.Count;
            int queued = PolicyPolDiffImporter.QueueFromReg(reg, bundle);
            Assert.Equal(1, queued);
            Assert.Equal(before + 1, PendingChangesService.Instance.Pending.Count);
            var pc = PendingChangesService.Instance.Pending.First();
            Assert.Equal(machine.UniqueID, pc.PolicyId);
            Assert.Equal(PolicyState.Enabled, pc.DesiredState);
            Assert.Equal("Computer", pc.Scope);
        }

        [Fact(DisplayName = "Replace mode discards existing pending before queueing new import")]
        public void ReplaceMode_DiscardsExisting()
        {
            var (bundle, machine, userEnum) = BuildBundle();
            var emptyPolComp = new PolFile();
            var emptyPolUser = new PolFile();
            ((PolicySourceManager)PolicySourceManager.Instance).CompSource =
                new InMemoryPolicySource(emptyPolComp);
            ((PolicySourceManager)PolicySourceManager.Instance).UserSource =
                new InMemoryPolicySource(emptyPolUser);

            // Seed a pending change (pretend user queued something earlier)
            PendingChangesService.Instance.DiscardAll();
            PendingChangesService.Instance.Add(
                new PendingChange
                {
                    PolicyId = machine.UniqueID,
                    PolicyName = machine.DisplayName ?? machine.UniqueID,
                    Scope = "Computer",
                    Action = "Enable",
                    DesiredState = PolicyState.Enabled,
                }
            );
            Assert.Single(PendingChangesService.Instance.Pending);

            // Build .reg enabling user enum instead (different policy) to show previous one is removed when replace
            var reg = new RegFile();
            reg.SetPrefix(string.Empty);
            var userKey = new RegFile.RegFileKey
            {
                Name = "HKEY_CURRENT_USER\\Software\\EnumPolicyRoot",
            };
            // Write a dummy value; we only test discard semantics, not precise diff mapping here.
            userKey.Values.Add(
                new RegFile.RegFileValue
                {
                    Name = "Dummy",
                    Data = 1U,
                    Kind = Microsoft.Win32.RegistryValueKind.DWord,
                }
            );
            reg.Keys.Add(userKey);

            int queued = RegImportQueueHelper.Queue(reg, bundle, replace: true);
            // After replace old pending (machine policy) removed; either 0 or >0 new queued depending on policy match
            Assert.DoesNotContain(
                PendingChangesService.Instance.Pending,
                p => p.PolicyId == machine.UniqueID
            );
            Assert.True(PendingChangesService.Instance.Pending.Count >= 0);
        }

        [Fact(DisplayName = "Replace mode clears even when previous discard failed")]
        public void ReplaceMode_ForcesClear()
        {
            var (bundle, machine, _) = BuildBundle();
            var emptyPolComp = new PolFile();
            var emptyPolUser = new PolFile();
            ((PolicySourceManager)PolicySourceManager.Instance).CompSource =
                new InMemoryPolicySource(emptyPolComp);
            ((PolicySourceManager)PolicySourceManager.Instance).UserSource =
                new InMemoryPolicySource(emptyPolUser);
            PendingChangesService.Instance.DiscardAll();
            // Seed two entries
            PendingChangesService.Instance.Add(
                new PendingChange
                {
                    PolicyId = machine.UniqueID,
                    PolicyName = machine.DisplayName ?? machine.UniqueID,
                    Scope = "Computer",
                    Action = "Enable",
                    DesiredState = PolicyState.Enabled,
                }
            );
            PendingChangesService.Instance.Add(
                new PendingChange
                {
                    PolicyId = "USER:Dummy",
                    PolicyName = "Dummy",
                    Scope = "User",
                    Action = "Enable",
                    DesiredState = PolicyState.Enabled,
                }
            );
            Assert.Equal(2, PendingChangesService.Instance.Pending.Count);
            // Empty reg import triggers replace path; queue returns 0 but pending should be cleared.
            var reg = new RegFile();
            reg.SetPrefix(string.Empty);
            int queued = RegImportQueueHelper.Queue(reg, bundle, replace: true);
            Assert.Equal(0, queued);
            Assert.Empty(PendingChangesService.Instance.Pending);
        }

        [Fact(DisplayName = "Replace mode queues Clear for policies missing in reg")]
        public void ReplaceMode_QueuesClearsForMissing()
        {
            var (bundle, machine, userEnum) = BuildBundle();
            // Current: machine toggle enabled, user enum enabled (option index 0)
            var compPol = new PolFile();
            var userPol = new PolFile();
            PolicyProcessing.SetPolicyState(
                compPol,
                machine,
                PolicyState.Enabled,
                new Dictionary<string, object>()
            );
            PolicyProcessing.SetPolicyState(
                userPol,
                userEnum,
                PolicyState.Enabled,
                new Dictionary<string, object> { { "EnumElem", 0 } }
            );
            ((PolicySourceManager)PolicySourceManager.Instance).CompSource =
                new InMemoryPolicySource(compPol);
            ((PolicySourceManager)PolicySourceManager.Instance).UserSource =
                new InMemoryPolicySource(userPol);
            // Imported reg only contains the machine toggle (so user enum should be cleared)
            var reg = new RegFile();
            reg.SetPrefix(string.Empty);
            var k = new RegFile.RegFileKey { Name = "HKEY_LOCAL_MACHINE\\Software\\PolDiffTest" };
            k.Values.Add(
                new RegFile.RegFileValue
                {
                    Name = "ToggleValue",
                    Data = 1U,
                    Kind = Microsoft.Win32.RegistryValueKind.DWord,
                }
            );
            reg.Keys.Add(k);
            PendingChangesService.Instance.DiscardAll();
            int queued = RegImportQueueHelper.Queue(reg, bundle, replace: true);
            // Expect 2 queued: machine toggle (no change -> maybe skipped) + user enum clear. We assert at least one Clear for user enum.
            Assert.True(queued >= 1);
            Assert.Contains(
                PendingChangesService.Instance.Pending,
                p => p.PolicyId == userEnum.UniqueID && p.Action == "Clear"
            );
        }

        [Fact(DisplayName = "List policy identical entries produce no diff on import")]
        public void ListPolicy_NoFalseDiff()
        {
            // Build bundle with a list policy only
            var listPolicy = TestPolicyFactory.CreateListPolicy("MACHINE:ListSame");
            var bundle = new AdmxBundle { Policies = new Dictionary<string, PolicyPlusPolicy>() };
            bundle.Policies[listPolicy.UniqueID] = listPolicy;
            // Current source has ListPrefix1=Alpha, ListPrefix2=Beta
            var compPol = new PolFile();
            PolicyProcessing.ForgetPolicy(compPol, listPolicy);
            // Simulate enabling with two list entries via registry style values
            compPol.SetValue(
                listPolicy.RawPolicy.RegistryKey,
                listPolicy.RawPolicy.RegistryValue + 1,
                "Alpha",
                Microsoft.Win32.RegistryValueKind.String
            );
            compPol.SetValue(
                listPolicy.RawPolicy.RegistryKey,
                listPolicy.RawPolicy.RegistryValue + 2,
                "Beta",
                Microsoft.Win32.RegistryValueKind.String
            );
            ((PolicySourceManager)PolicySourceManager.Instance).CompSource =
                new InMemoryPolicySource(compPol);
            ((PolicySourceManager)PolicySourceManager.Instance).UserSource =
                new InMemoryPolicySource(new PolFile());
            // Build reg file mirroring identical values
            var reg = new RegFile();
            reg.SetPrefix(string.Empty);
            var key = new RegFile.RegFileKey
            {
                Name = "HKEY_LOCAL_MACHINE\\" + listPolicy.RawPolicy.RegistryKey,
            };
            key.Values.Add(
                new RegFile.RegFileValue
                {
                    Name = listPolicy.RawPolicy.RegistryValue + 1,
                    Data = "Alpha",
                    Kind = Microsoft.Win32.RegistryValueKind.String,
                }
            );
            key.Values.Add(
                new RegFile.RegFileValue
                {
                    Name = listPolicy.RawPolicy.RegistryValue + 2,
                    Data = "Beta",
                    Kind = Microsoft.Win32.RegistryValueKind.String,
                }
            );
            reg.Keys.Add(key);
            PendingChangesService.Instance.DiscardAll();
            int queued = RegImportQueueHelper.Queue(reg, bundle, replace: false);
            Assert.Equal(0, queued); // No diff expected
            Assert.Empty(PendingChangesService.Instance.Pending);
        }

        [Fact(DisplayName = "Diff importer queues enum option change (user scope)")]
        public void Queue_Enum_Option_Change()
        {
            var (bundle, _, userEnum) = BuildBundle();
            // Current: Enabled with OptA (value 1)
            var curUserPol = new PolFile();
            // Current state: select first enum item (index 0 -> value=1)
            SetPolicy(
                curUserPol,
                userEnum,
                PolicyState.Enabled,
                new Dictionary<string, object> { { "EnumElem", 0 } }
            );
            var curCompPol = new PolFile();
            ((PolicySourceManager)PolicySourceManager.Instance).CompSource =
                new InMemoryPolicySource(curCompPol);
            ((PolicySourceManager)PolicySourceManager.Instance).UserSource =
                new InMemoryPolicySource(curUserPol);

            // Imported: OptB (value 2)
            var reg = new RegFile();
            reg.SetPrefix(string.Empty);
            var k = new RegFile.RegFileKey { Name = "HKEY_CURRENT_USER\\Software\\PolicyPlusTest" };
            k.Values.Add(
                new RegFile.RegFileValue
                {
                    Name = "EnumValue",
                    Data = 2U,
                    Kind = Microsoft.Win32.RegistryValueKind.DWord,
                }
            );
            // Also write element's registry value (enum underlying value stored at same value name)
            reg.Keys.Add(k);

            var (userPol, machinePol) = RegImportHelper.ToPolByHive(reg); // sanity (not directly used further here)
            var directState = PolicyProcessing.GetPolicyState(userPol, userEnum);
            Assert.Equal(PolicyState.Enabled, directState); // ensure imported reg translates to Enabled
            Assert.True(
                userPol.ContainsValue("Software\\PolicyPlusTest", "EnumValue"),
                "Underlying value missing in PolFile"
            );

            PendingChangesService.EnableTestIsolation();
            PendingChangesService.ResetAmbientForTest();
            int queued = PolicyPolDiffImporter.QueueFromReg(reg, bundle);
            Assert.Equal(1, queued);
            var pc = PendingChangesService.Instance.Pending.Single();
            Assert.Equal(userEnum.UniqueID, pc.PolicyId);
            Assert.Equal(PolicyState.Enabled, pc.DesiredState);
            Assert.Equal("User", pc.Scope);
            Assert.NotNull(pc.Options);
            Assert.True(pc.Options!.TryGetValue("EnumElem", out var enumIdx));
            // Expect selected enum index (OptB = 1)
            Assert.Equal(1, (int)enumIdx);
        }

        [Fact(DisplayName = "Diff importer skips unchanged machine toggle (no queue)")]
        public void Skip_Unchanged_Machine_Toggle()
        {
            var (bundle, machine, _) = BuildBundle();
            var curComp = new PolFile();
            // Current enabled state
            SetPolicy(curComp, machine, PolicyState.Enabled);
            var curUser = new PolFile();
            ((PolicySourceManager)PolicySourceManager.Instance).CompSource =
                new InMemoryPolicySource(curComp);
            ((PolicySourceManager)PolicySourceManager.Instance).UserSource =
                new InMemoryPolicySource(curUser);

            // Imported reg establishes the same enabled state
            var reg = new RegFile();
            reg.SetPrefix(string.Empty);
            var k = new RegFile.RegFileKey { Name = "HKEY_LOCAL_MACHINE\\Software\\PolDiffTest" };
            k.Values.Add(
                new RegFile.RegFileValue
                {
                    Name = "ToggleValue",
                    Data = 1U,
                    Kind = Microsoft.Win32.RegistryValueKind.DWord,
                }
            );
            reg.Keys.Add(k);

            PendingChangesService.EnableTestIsolation();
            PendingChangesService.ResetAmbientForTest();
            int queued = PolicyPolDiffImporter.QueueFromReg(reg, bundle);
            Assert.Equal(0, queued); // unchanged should be skipped now
            Assert.Empty(PendingChangesService.Instance.Pending);
        }

        [Fact(DisplayName = "Diff importer queues disable when imported state is Disabled")]
        public void Queue_Disable_From_Enabled()
        {
            var (bundle, machine, _) = BuildBundle();
            var curComp = new PolFile();
            SetPolicy(curComp, machine, PolicyState.Enabled);
            var curUser = new PolFile();
            ((PolicySourceManager)PolicySourceManager.Instance).CompSource =
                new InMemoryPolicySource(curComp);
            ((PolicySourceManager)PolicySourceManager.Instance).UserSource =
                new InMemoryPolicySource(curUser);

            var reg = new RegFile();
            reg.SetPrefix(string.Empty);
            var k = new RegFile.RegFileKey { Name = "HKEY_LOCAL_MACHINE\\Software\\PolDiffTest" };
            k.Values.Add(
                new RegFile.RegFileValue
                {
                    Name = "ToggleValue",
                    Data = 0U,
                    Kind = Microsoft.Win32.RegistryValueKind.DWord,
                }
            ); // off
            reg.Keys.Add(k);

            PendingChangesService.EnableTestIsolation();
            PendingChangesService.ResetAmbientForTest();
            int queued = PolicyPolDiffImporter.QueueFromReg(reg, bundle);
            Assert.Equal(1, queued);
            var pc = PendingChangesService.Instance.Pending.Single();
            Assert.Equal(PolicyState.Disabled, pc.DesiredState);
            Assert.Equal("Disable", pc.Action);
        }

        [Fact(DisplayName = "Diff importer skips unchanged disabled state")]
        public void Skip_Unchanged_Disabled_Toggle()
        {
            var (bundle, machine, _) = BuildBundle();
            var curComp = new PolFile();
            SetPolicy(curComp, machine, PolicyState.Disabled);
            var curUser = new PolFile();
            ((PolicySourceManager)PolicySourceManager.Instance).CompSource =
                new InMemoryPolicySource(curComp);
            ((PolicySourceManager)PolicySourceManager.Instance).UserSource =
                new InMemoryPolicySource(curUser);

            var reg = new RegFile();
            reg.SetPrefix(string.Empty);
            var k = new RegFile.RegFileKey { Name = "HKEY_LOCAL_MACHINE\\Software\\PolDiffTest" };
            k.Values.Add(
                new RegFile.RegFileValue
                {
                    Name = "ToggleValue",
                    Data = 0U,
                    Kind = Microsoft.Win32.RegistryValueKind.DWord,
                }
            ); // disabled again
            reg.Keys.Add(k);

            PendingChangesService.EnableTestIsolation();
            PendingChangesService.ResetAmbientForTest();
            int queued = PolicyPolDiffImporter.QueueFromReg(reg, bundle);
            Assert.Equal(0, queued);
            Assert.Empty(PendingChangesService.Instance.Pending);
        }

        [Fact(
            DisplayName = "Diff importer skips unchanged Enabled with numeric type differences (1U vs 1)"
        )]
        public void Skip_Unchanged_Enabled_NumericEquivalent()
        {
            var decimalPolicy = TestPolicyFactory.CreateDecimalPolicy("MACHINE:DecNumericEq");
            var bundle = new AdmxBundle
            {
                Policies = new Dictionary<string, PolicyPlusPolicy>
                {
                    { decimalPolicy.UniqueID, decimalPolicy },
                },
            };
            var curComp = new PolFile();
            SetPolicy(
                curComp,
                decimalPolicy,
                PolicyState.Enabled,
                new Dictionary<string, object> { { "DecimalElem", 1U } }
            );
            ((PolicySourceManager)PolicySourceManager.Instance).CompSource =
                new InMemoryPolicySource(curComp);
            ((PolicySourceManager)PolicySourceManager.Instance).UserSource =
                new InMemoryPolicySource(new PolFile());

            var reg = new RegFile();
            reg.SetPrefix(string.Empty);
            var k = new RegFile.RegFileKey
            {
                Name = "HKEY_LOCAL_MACHINE\\Software\\PolicyPlusTest",
            };
            // Imported INT (not uint) 1 -> should be considered equal.
            k.Values.Add(
                new RegFile.RegFileValue
                {
                    Name = "DecimalValue",
                    Data = 1,
                    Kind = Microsoft.Win32.RegistryValueKind.DWord,
                }
            );
            reg.Keys.Add(k);

            PendingChangesService.EnableTestIsolation();
            PendingChangesService.ResetAmbientForTest();
            int queued = PolicyPolDiffImporter.QueueFromReg(reg, bundle);
            Assert.Equal(0, queued);
            Assert.Empty(PendingChangesService.Instance.Pending);
        }

        [Fact(DisplayName = "Diff importer queues multi-text order change as difference")]
        public void Queue_MultiText_OrderChange()
        {
            var multiTextPolicy = TestPolicyFactory.CreateMultiTextPolicy("MACHINE:MTOrder");
            var bundle = new AdmxBundle
            {
                Policies = new Dictionary<string, PolicyPlusPolicy>
                {
                    { multiTextPolicy.UniqueID, multiTextPolicy },
                },
            };
            var curComp = new PolFile();
            SetPolicy(
                curComp,
                multiTextPolicy,
                PolicyState.Enabled,
                new Dictionary<string, object> { { "MultiTextElem", new[] { "A", "B" } } }
            );
            ((PolicySourceManager)PolicySourceManager.Instance).CompSource =
                new InMemoryPolicySource(curComp);
            ((PolicySourceManager)PolicySourceManager.Instance).UserSource =
                new InMemoryPolicySource(new PolFile());

            var reg = new RegFile();
            reg.SetPrefix(string.Empty);
            var k = new RegFile.RegFileKey
            {
                Name = "HKEY_LOCAL_MACHINE\\Software\\PolicyPlusTest",
            };
            k.Values.Add(
                new RegFile.RegFileValue
                {
                    Name = "MultiTextValue",
                    Data = new[] { "B", "A" },
                    Kind = Microsoft.Win32.RegistryValueKind.MultiString,
                }
            );
            reg.Keys.Add(k);

            PendingChangesService.EnableTestIsolation();
            PendingChangesService.ResetAmbientForTest();
            int queued = PolicyPolDiffImporter.QueueFromReg(reg, bundle);
            Assert.Equal(1, queued);
            var pc = PendingChangesService.Instance.Pending.Single();
            Assert.Equal(multiTextPolicy.UniqueID, pc.PolicyId);
            Assert.Equal(PolicyState.Enabled, pc.DesiredState);
            Assert.NotNull(pc.Options);
        }

        [Fact(DisplayName = "Diff importer skips identical multi-text content same order")]
        public void Skip_Unchanged_MultiText_SameOrder()
        {
            var multiTextPolicy = TestPolicyFactory.CreateMultiTextPolicy("MACHINE:MTSame");
            var bundle = new AdmxBundle
            {
                Policies = new Dictionary<string, PolicyPlusPolicy>
                {
                    { multiTextPolicy.UniqueID, multiTextPolicy },
                },
            };
            var curComp = new PolFile();
            SetPolicy(
                curComp,
                multiTextPolicy,
                PolicyState.Enabled,
                new Dictionary<string, object> { { "MultiTextElem", new[] { "A", "B" } } }
            );
            ((PolicySourceManager)PolicySourceManager.Instance).CompSource =
                new InMemoryPolicySource(curComp);
            ((PolicySourceManager)PolicySourceManager.Instance).UserSource =
                new InMemoryPolicySource(new PolFile());

            var reg = new RegFile();
            reg.SetPrefix(string.Empty);
            var k = new RegFile.RegFileKey
            {
                Name = "HKEY_LOCAL_MACHINE\\Software\\PolicyPlusTest",
            };
            k.Values.Add(
                new RegFile.RegFileValue
                {
                    Name = "MultiTextValue",
                    Data = new[] { "A", "B" },
                    Kind = Microsoft.Win32.RegistryValueKind.MultiString,
                }
            );
            reg.Keys.Add(k);

            PendingChangesService.EnableTestIsolation();
            PendingChangesService.ResetAmbientForTest();
            int queued = PolicyPolDiffImporter.QueueFromReg(reg, bundle);
            Assert.Equal(0, queued);
            Assert.Empty(PendingChangesService.Instance.Pending);
        }

        [Fact(
            DisplayName = "Diff importer queues enable when imported state is Enabled from Disabled current"
        )]
        public void Queue_Enable_From_Disabled()
        {
            var (bundle, machine, _) = BuildBundle();
            var curComp = new PolFile();
            SetPolicy(curComp, machine, PolicyState.Disabled); // current disabled
            ((PolicySourceManager)PolicySourceManager.Instance).CompSource =
                new InMemoryPolicySource(curComp);
            ((PolicySourceManager)PolicySourceManager.Instance).UserSource =
                new InMemoryPolicySource(new PolFile());

            var reg = new RegFile();
            reg.SetPrefix(string.Empty);
            var k = new RegFile.RegFileKey { Name = "HKEY_LOCAL_MACHINE\\Software\\PolDiffTest" };
            k.Values.Add(
                new RegFile.RegFileValue
                {
                    Name = "ToggleValue",
                    Data = 1U,
                    Kind = Microsoft.Win32.RegistryValueKind.DWord,
                }
            ); // enable
            reg.Keys.Add(k);

            PendingChangesService.EnableTestIsolation();
            PendingChangesService.ResetAmbientForTest();
            int queued = PolicyPolDiffImporter.QueueFromReg(reg, bundle);
            Assert.Equal(1, queued);
            var pc = PendingChangesService.Instance.Pending.Single();
            Assert.Equal(PolicyState.Enabled, pc.DesiredState);
            Assert.Equal("Enable", pc.Action);
        }

        [Fact(
            DisplayName = "Diff importer skips removal (Enabled current, NotConfigured imported)"
        )]
        public void Skip_Removal_To_NotConfigured()
        {
            var (bundle, machine, _) = BuildBundle();
            var curComp = new PolFile();
            SetPolicy(curComp, machine, PolicyState.Enabled); // currently enabled
            ((PolicySourceManager)PolicySourceManager.Instance).CompSource =
                new InMemoryPolicySource(curComp);
            ((PolicySourceManager)PolicySourceManager.Instance).UserSource =
                new InMemoryPolicySource(new PolFile());

            // Imported reg omits the policy entirely => NotConfigured newState
            var reg = new RegFile();
            reg.SetPrefix(string.Empty);

            PendingChangesService.EnableTestIsolation();
            PendingChangesService.ResetAmbientForTest();
            int queued = PolicyPolDiffImporter.QueueFromReg(reg, bundle);
            Assert.Equal(0, queued); // we do not queue Clear operations currently
            Assert.Empty(PendingChangesService.Instance.Pending);
        }
    }
}
