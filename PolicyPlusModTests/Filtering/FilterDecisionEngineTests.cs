using PolicyPlusPlus.Filtering;
using Xunit;

namespace PolicyPlusModTests.Filtering
{
    public class FilterDecisionEngineTests
    {
        // Helper to assert table row
        private void AssertRow(
            int row,
            bool hasCategory,
            bool search,
            bool configuredOnly,
            bool bookmarkOnly,
            bool expectedFlatten,
            bool expectedShowHeaders,
            bool expectedIncludeSubcats,
            int? expectedLimit,
            bool limitSettingEnabled = true
        )
        {
            var r = FilterDecisionEngine.Evaluate(
                hasCategory,
                search,
                configuredOnly,
                bookmarkOnly,
                limitSettingEnabled
            );
            Assert.Equal(expectedFlatten, r.FlattenHierarchy); // row {row}
            Assert.Equal(expectedShowHeaders, r.ShowSubcategoryHeaders);
            Assert.Equal(expectedIncludeSubcats, r.IncludeSubcategoryPolicies);
            Assert.Equal(expectedLimit, r.Limit);
        }

        [Fact(DisplayName = "Truth table row 1")]
        public void Row1() =>
            AssertRow(
                1,
                hasCategory: false,
                search: false,
                configuredOnly: false,
                bookmarkOnly: false,
                expectedFlatten: false,
                expectedShowHeaders: false,
                expectedIncludeSubcats: true,
                expectedLimit: 1000
            );

        [Fact(DisplayName = "Truth table row 2")]
        public void Row2() =>
            AssertRow(
                2,
                hasCategory: true,
                search: false,
                configuredOnly: false,
                bookmarkOnly: false,
                expectedFlatten: false,
                expectedShowHeaders: true,
                expectedIncludeSubcats: false,
                expectedLimit: null
            );

        [Fact(DisplayName = "Truth table row 3")]
        public void Row3() =>
            AssertRow(
                3,
                hasCategory: false,
                search: true,
                configuredOnly: false,
                bookmarkOnly: false,
                expectedFlatten: true,
                expectedShowHeaders: false,
                expectedIncludeSubcats: true,
                expectedLimit: 1000
            );

        [Fact(DisplayName = "Truth table row 4")]
        public void Row4() =>
            AssertRow(
                4,
                hasCategory: true,
                search: true,
                configuredOnly: false,
                bookmarkOnly: false,
                expectedFlatten: true,
                expectedShowHeaders: false,
                expectedIncludeSubcats: true,
                expectedLimit: null
            );

        [Fact(DisplayName = "Truth table row 5")]
        public void Row5() =>
            AssertRow(
                5,
                hasCategory: false,
                search: false,
                configuredOnly: true,
                bookmarkOnly: false,
                expectedFlatten: true,
                expectedShowHeaders: false,
                expectedIncludeSubcats: true,
                expectedLimit: null
            );

        [Fact(DisplayName = "Truth table row 6")]
        public void Row6() =>
            AssertRow(
                6,
                hasCategory: true,
                search: false,
                configuredOnly: true,
                bookmarkOnly: false,
                expectedFlatten: true,
                expectedShowHeaders: false,
                expectedIncludeSubcats: true,
                expectedLimit: null
            );

        [Fact(DisplayName = "Truth table row 7")]
        public void Row7() =>
            AssertRow(
                7,
                hasCategory: false,
                search: true,
                configuredOnly: true,
                bookmarkOnly: false,
                expectedFlatten: true,
                expectedShowHeaders: false,
                expectedIncludeSubcats: true,
                expectedLimit: null
            );

        [Fact(DisplayName = "Truth table row 8")]
        public void Row8() =>
            AssertRow(
                8,
                hasCategory: true,
                search: true,
                configuredOnly: true,
                bookmarkOnly: false,
                expectedFlatten: true,
                expectedShowHeaders: false,
                expectedIncludeSubcats: true,
                expectedLimit: null
            );

        [Fact(DisplayName = "Truth table row 9")]
        public void Row9() =>
            AssertRow(
                9,
                hasCategory: false,
                search: false,
                configuredOnly: false,
                bookmarkOnly: true,
                expectedFlatten: true,
                expectedShowHeaders: false,
                expectedIncludeSubcats: true,
                expectedLimit: null
            );

        [Fact(DisplayName = "Truth table row 10")]
        public void Row10() =>
            AssertRow(
                10,
                hasCategory: true,
                search: false,
                configuredOnly: false,
                bookmarkOnly: true,
                expectedFlatten: true,
                expectedShowHeaders: false,
                expectedIncludeSubcats: true,
                expectedLimit: null
            );

        [Fact(DisplayName = "Truth table row 11")]
        public void Row11() =>
            AssertRow(
                11,
                hasCategory: false,
                search: true,
                configuredOnly: false,
                bookmarkOnly: true,
                expectedFlatten: true,
                expectedShowHeaders: false,
                expectedIncludeSubcats: true,
                expectedLimit: null
            );

        [Fact(DisplayName = "Truth table row 12")]
        public void Row12() =>
            AssertRow(
                12,
                hasCategory: true,
                search: true,
                configuredOnly: false,
                bookmarkOnly: true,
                expectedFlatten: true,
                expectedShowHeaders: false,
                expectedIncludeSubcats: true,
                expectedLimit: null
            );

        [Fact(DisplayName = "Truth table row 13")]
        public void Row13() =>
            AssertRow(
                13,
                hasCategory: false,
                search: false,
                configuredOnly: true,
                bookmarkOnly: true,
                expectedFlatten: true,
                expectedShowHeaders: false,
                expectedIncludeSubcats: true,
                expectedLimit: null
            );

        [Fact(DisplayName = "Truth table row 14")]
        public void Row14() =>
            AssertRow(
                14,
                hasCategory: true,
                search: false,
                configuredOnly: true,
                bookmarkOnly: true,
                expectedFlatten: true,
                expectedShowHeaders: false,
                expectedIncludeSubcats: true,
                expectedLimit: null
            );

        [Fact(DisplayName = "Truth table row 15")]
        public void Row15() =>
            AssertRow(
                15,
                hasCategory: false,
                search: true,
                configuredOnly: true,
                bookmarkOnly: true,
                expectedFlatten: true,
                expectedShowHeaders: false,
                expectedIncludeSubcats: true,
                expectedLimit: null
            );

        [Fact(DisplayName = "Truth table row 16")]
        public void Row16() =>
            AssertRow(
                16,
                hasCategory: true,
                search: true,
                configuredOnly: true,
                bookmarkOnly: true,
                expectedFlatten: true,
                expectedShowHeaders: false,
                expectedIncludeSubcats: true,
                expectedLimit: null
            );

        // Additional test verifying row1/row3 limit is omitted if setting disabled
        [Fact(DisplayName = "Row1 limit disabled by setting")]
        public void Row1_NoLimitSetting() =>
            AssertRow(
                1,
                false,
                false,
                false,
                false,
                expectedFlatten: false,
                expectedShowHeaders: false,
                expectedIncludeSubcats: true,
                expectedLimit: null,
                limitSettingEnabled: false
            );

        [Fact(DisplayName = "Row3 limit disabled by setting")]
        public void Row3_NoLimitSetting() =>
            AssertRow(
                3,
                false,
                true,
                false,
                false,
                expectedFlatten: true,
                expectedShowHeaders: false,
                expectedIncludeSubcats: true,
                expectedLimit: null,
                limitSettingEnabled: false
            );
    }
}
