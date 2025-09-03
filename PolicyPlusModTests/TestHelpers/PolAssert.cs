using System.Collections.Generic;
using System.Linq;

using PolicyPlus.Core.IO;

using Xunit;

namespace PolicyPlusModTests.TestHelpers
{
    // Assertion helpers for POL file verification
    public static class PolAssert
    {
        public static void HasDwordValue(PolFile pol, string key, string valueName, uint expected)
        {
            Assert.True(pol.ContainsValue(key, valueName));
            var value = pol.GetValue(key, valueName);
            Assert.IsType<uint>(value);
            Assert.Equal(expected, (uint)value);
        }

        public static void HasStringValue(PolFile pol, string key, string valueName, string expected)
        {
            Assert.True(pol.ContainsValue(key, valueName));
            var value = pol.GetValue(key, valueName);
            Assert.Equal(expected, value as string);
        }

        public static void HasMultiStringValue(PolFile pol, string key, string valueName, IEnumerable<string> expected)
        {
            Assert.True(pol.ContainsValue(key, valueName));
            var value = pol.GetValue(key, valueName) as string[];
            Assert.NotNull(value);
            Assert.True(expected.SequenceEqual(value));
        }

        public static void HasSequentialListValues(PolFile pol, string key, string prefix, IList<string> expected)
        {
            for (int i = 0; i < expected.Count; i++)
            {
                string valueName = prefix + (i + 1);
                HasStringValue(pol, key, valueName, expected[i]);
            }
        }

        public static void NotContains(PolFile pol, string key, string valueName)
        {
            Assert.False(pol.ContainsValue(key, valueName));
        }
    }
}
