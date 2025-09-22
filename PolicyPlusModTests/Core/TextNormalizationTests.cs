using PolicyPlusCore.Utilities;
using Xunit;

namespace PolicyPlusModTests.Core;

public class TextNormalizationTests
{
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("ABC-123")]
    [InlineData("あいうえお")]
    [InlineData("ｶﾀｶﾅ")]
    [InlineData("Windows—Policy")]
    public void Loose_FromStrict_Equals_Loose_FromRaw(string input)
    {
        var strict = TextNormalization.NormalizeStrict(input);
        var loose1 = TextNormalization.NormalizeLoose(input);
        var loose2 = TextNormalization.NormalizeLooseFromStrict(strict);
        Assert.Equal(loose1, loose2);
    }

    [Theory]
    [InlineData("Policy")]
    [InlineData("Group Policy")]
    [InlineData("レジストリ")]
    [InlineData("HKLM\\Software\\Policies")]
    public void ToNGramTokens_Is_Deterministic(string input)
    {
        var strict = TextNormalization.NormalizeStrict(input);
        var grams1 = TextNormalization.ToNGramTokens(strict);
        var grams2 = TextNormalization.ToNGramTokens(strict);
        Assert.Equal(grams1, grams2);
    }
}
