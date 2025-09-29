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
