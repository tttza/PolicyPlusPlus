using PolicyPlusModTests.Testing;
using PolicyPlusPlus.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace PolicyPlusModTests.WinUI3.PendingChanges
{
    // Validates that after serializing History to JSON and deserializing back, option types are normalized for reapply.
    public class NormalizeOptionsJsonRoundTripTests
    {
        private static (PolicyPlusPolicy policy, Dictionary<string, object> opts) BuildNamedListCase()
        {
            var pol = TestPolicyFactory.CreateNamedListPolicy("MACHINE:NormNamedList");
            var opts = new Dictionary<string, object>
            {
                { "NamedListElem", new List<KeyValuePair<string,string>>{ new("K1","V1"), new("K2","V2") } }
            };
            return (pol, opts);
        }

        private static (PolicyPlusPolicy policy, Dictionary<string, object> opts) BuildMultiTextCase()
        {
            var pol = TestPolicyFactory.CreateMultiTextPolicy("MACHINE:NormMultiText");
            var opts = new Dictionary<string, object> { { "MultiTextElem", new[] { "L1", "L2" } } };
            return (pol, opts);
        }

        private static (PolicyPlusPolicy policy, Dictionary<string, object> opts) BuildEnumCase()
        {
            var pol = TestPolicyFactory.CreateEnumPolicy("MACHINE:NormEnum");
            var opts = new Dictionary<string, object> { { "EnumElem", 1 } };
            return (pol, opts);
        }

        private static (PolicyPlusPolicy policy, Dictionary<string, object> opts) BuildDecimalCase()
        {
            var pol = TestPolicyFactory.CreateDecimalPolicy("MACHINE:NormDecimal");
            var opts = new Dictionary<string, object> { { "DecimalElem", 42u } };
            return (pol, opts);
        }

        private static HistoryRecord ApplyAndRecord(PolicyPlusPolicy policy, Dictionary<string, object> opts)
        {
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
            return PendingChangesService.Instance.History.First();
        }

        private static HistoryRecord RoundTrip(HistoryRecord h)
        {
            var json = JsonSerializer.Serialize(h);
            return JsonSerializer.Deserialize<HistoryRecord>(json)!;
        }

        [Theory]
        [InlineData("NamedList")]
        [InlineData("MultiText")]
        [InlineData("Enum")]
        [InlineData("Decimal")]
        public void Options_Normalize_From_Json(string kind)
        {
            PolicyPlusPolicy pol; Dictionary<string, object> opts;
            switch (kind)
            {
                case "NamedList": (pol, opts) = BuildNamedListCase(); break;
                case "MultiText": (pol, opts) = BuildMultiTextCase(); break;
                case "Enum": (pol, opts) = BuildEnumCase(); break;
                case "Decimal": (pol, opts) = BuildDecimalCase(); break;
                default: throw new InvalidOperationException();
            }
            var original = ApplyAndRecord(pol, opts);
            var roundTripped = RoundTrip(original);
            // simulate reapply path normalization using reflection (internal NormalizeOptions is private)
            var windowType = typeof(PolicyPlusPlus.Windows.PendingChangesWindow);
            var mi = windowType.GetMethod("NormalizeOptions", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.NotNull(mi);
            var normalized = (Dictionary<string, object>?)mi!.Invoke(null, new object?[] { roundTripped.Options });
            Assert.NotNull(normalized);
            foreach (var kv in opts)
            {
                Assert.True(normalized!.ContainsKey(kv.Key));
                var value = normalized[kv.Key];
                if (kv.Value is IEnumerable<KeyValuePair<string, string>> kvps)
                {
                    var list = (value as IEnumerable<KeyValuePair<string, string>>)?.ToList();
                    Assert.NotNull(list);
                    Assert.Equal(kvps.Count(), list!.Count);
                }
                else if (kv.Value is IEnumerable<string> lines && kv.Key.Contains("MultiText"))
                {
                    var arr = value as IEnumerable<string>;
                    Assert.NotNull(arr);
                    Assert.True(lines.SequenceEqual(arr!));
                }
                else if (kv.Value is int i)
                {
                    Assert.Equal(i, (int)value);
                }
                else if (kv.Value is uint u)
                {
                    Assert.Equal(u, Convert.ToUInt32(value));
                }
            }
        }
    }
}
