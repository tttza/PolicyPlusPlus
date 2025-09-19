using System;
using PolicyPlusPlus.Converters;
using Xunit;

namespace PolicyPlusModTests.WinUI3.Converters
{
    public class ConvertersTests
    {
        [Theory]
        [InlineData(true, "Visible", true)]
        [InlineData(false, "Collapsed", false)]
        [InlineData(null, "Collapsed", false)]
        public void BoolToVisibility_Convert_And_Back(
            object? input,
            string expectedName,
            bool expectedBack
        )
        {
            var conv = new BoolToVisibilityConverter();
            var result = conv.Convert(input!, null!, null!, "");
            Assert.Equal(expectedName, result!.ToString());

            var back = (bool)conv.ConvertBack(result!, typeof(bool), null!, "");
            Assert.Equal(expectedBack, back);
        }

        [Theory]
        [InlineData(true, "Collapsed", false)]
        [InlineData(false, "Visible", true)]
        [InlineData(null, "Visible", true)]
        public void NotBoolToVisibility_Convert_And_Back(
            object? input,
            string expectedName,
            bool expectedBack
        )
        {
            var conv = new NotBoolToVisibilityConverter();
            var result = conv.Convert(input!, null!, null!, "");
            Assert.Equal(expectedName, result!.ToString());

            var back = (bool)conv.ConvertBack(result!, typeof(bool), null!, "");
            Assert.Equal(expectedBack, back);
        }

        [Theory]
        [InlineData(false, 0.0)]
        [InlineData(true, 90.0)]
        [InlineData(null, 0.0)]
        public void BoolToAngle_Convert(object? input, double expected)
        {
            var conv = new BoolToAngleConverter();
            var result = Convert.ToDouble(conv.Convert(input!, typeof(double), null!, ""));
            Assert.Equal(expected, result, 3);
        }

        [Theory]
        [InlineData(0.0, false)]
        [InlineData(90.0, true)]
        [InlineData(89.95, true)]
        [InlineData(45.0, false)]
        [InlineData("90", true)]
        [InlineData("abc", false)]
        public void BoolToAngle_ConvertBack(object input, bool expected)
        {
            var conv = new BoolToAngleConverter();
            var result = (bool)conv.ConvertBack(input!, typeof(bool), null!, "");
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(true, false, "\uE73E")] // Accept
        [InlineData(false, true, "\uE711")] // Block
        [InlineData(false, false, "")]
        public void BoolPairToGlyph_Convert(bool enabled, bool disabled, string expected)
        {
            var conv = new BoolPairToGlyphConverter();
            var tuple = (enabled, disabled);
            var result = (string)conv.Convert(tuple, typeof(string), null!, "");
            Assert.Equal(expected, result);
        }

        [Fact]
        public void BoolPairToGlyph_ConvertBack_NotSupported()
        {
            var conv = new BoolPairToGlyphConverter();
            Assert.Throws<NotSupportedException>(() =>
            {
                var _ = conv.ConvertBack("", typeof(ValueTuple<bool, bool>), null!, "");
            });
        }
    }
}
