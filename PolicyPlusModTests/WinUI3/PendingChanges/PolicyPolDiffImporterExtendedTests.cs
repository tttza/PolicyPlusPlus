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
    public class PolicyPolDiffImporterExtendedTests
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

        [Fact(DisplayName = "Diff importer queues decimal value change")]
        public void Queue_Decimal_Change()
        {
            var decimalPolicy = TestPolicyFactory.CreateDecimalPolicy("MACHINE:DecChange");
            var bundle = Bundle(decimalPolicy);
            var curComp = new PolFile();
            // Current value = 10
            SetPolicy(
                curComp,
                decimalPolicy,
                PolicyState.Enabled,
                new Dictionary<string, object> { { "DecimalElem", 10U } }
            );
            ((PolicySourceManager)PolicySourceManager.Instance).CompSource =
                new InMemoryPolicySource(curComp);
            ((PolicySourceManager)PolicySourceManager.Instance).UserSource =
                new InMemoryPolicySource(new PolFile());

            // Imported: value = 42
            var reg = new RegFile();
            reg.SetPrefix(string.Empty);
            var key = new RegFile.RegFileKey
            {
                Name = "HKEY_LOCAL_MACHINE\\Software\\PolicyPlusTest",
            };
            key.Values.Add(
                new RegFile.RegFileValue
                {
                    Name = "DecimalValue",
                    Data = 42U,
                    Kind = Microsoft.Win32.RegistryValueKind.DWord,
                }
            );
            reg.Keys.Add(key);

            PendingChangesService.EnableTestIsolation();
            PendingChangesService.ResetAmbientForTest();
            int queued = PolicyPolDiffImporter.QueueFromReg(reg, bundle);
            Assert.Equal(1, queued);
            var pc = PendingChangesService.Instance.Pending.Single();
            Assert.Equal(decimalPolicy.UniqueID, pc.PolicyId);
            Assert.Equal(PolicyState.Enabled, pc.DesiredState);
            Assert.NotNull(pc.Options);
            Assert.True(pc.Options!.TryGetValue("DecimalElem", out var decVal));
            Assert.Equal(42U, Convert.ToUInt32(decVal));
        }

        [Fact(DisplayName = "Diff importer queues multiText change")]
        public void Queue_MultiText_Change()
        {
            var multiTextPolicy = TestPolicyFactory.CreateMultiTextPolicy("MACHINE:MultiTxtChange");
            var bundle = Bundle(multiTextPolicy);
            var curComp = new PolFile();
            // Current lines = A,B
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

            // Imported lines = A,B,C
            var reg = new RegFile();
            reg.SetPrefix(string.Empty);
            var key = new RegFile.RegFileKey
            {
                Name = "HKEY_LOCAL_MACHINE\\Software\\PolicyPlusTest",
            };
            key.Values.Add(
                new RegFile.RegFileValue
                {
                    Name = "MultiTextValue",
                    Data = new[] { "A", "B", "C" },
                    Kind = Microsoft.Win32.RegistryValueKind.MultiString,
                }
            );
            reg.Keys.Add(key);

            PendingChangesService.EnableTestIsolation();
            PendingChangesService.ResetAmbientForTest();
            int queued = PolicyPolDiffImporter.QueueFromReg(reg, bundle);
            Assert.Equal(1, queued);
            var pc = PendingChangesService.Instance.Pending.Single();
            Assert.Equal(multiTextPolicy.UniqueID, pc.PolicyId);
            Assert.Equal(PolicyState.Enabled, pc.DesiredState);
            Assert.NotNull(pc.Options);
            Assert.True(pc.Options!.TryGetValue("MultiTextElem", out var mtVal));
            var arr = (IEnumerable<string>)mtVal;
            Assert.True(arr.SequenceEqual(new[] { "A", "B", "C" }));
        }
    }
}
