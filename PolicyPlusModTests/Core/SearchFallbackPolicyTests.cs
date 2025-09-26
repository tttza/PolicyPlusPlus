using System.Collections.Generic;
using PolicyPlusCore.Utilities;
using Xunit;

namespace PolicyPlusModTests.Core;

public class SearchFallbackPolicyTests
{
    [Theory]
    [InlineData(1, true, false)] // single token, real second -> do not skip
    [InlineData(2, false, false)] // multi token -> do not skip
    [InlineData(3, false, false)] // multi token still no skip
    public void ShouldSkipMemoryFallback_NonStrict(
        int tokenCount,
        bool secondEnabled,
        bool expected
    )
    {
        var slots = CulturePreference.Build(
            new CulturePreference.BuildOptions(
                Primary: "en-US",
                Second: secondEnabled ? "fr-FR" : null,
                SecondEnabled: secondEnabled,
                OsUiCulture: "ja-JP",
                EnablePrimaryFallback: true
            )
        );
        var actual = SearchFallbackPolicy.ShouldSkipMemoryFallback(tokenCount, slots);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ShouldSkipMemoryFallback_PlaceholderSecond_True()
    {
        // Second enabled but same as primary -> placeholder.
        var slots = CulturePreference.Build(
            new CulturePreference.BuildOptions(
                Primary: "en-US",
                Second: "en-US",
                SecondEnabled: true,
                OsUiCulture: "ja-JP",
                EnablePrimaryFallback: true
            )
        );
        var actual = SearchFallbackPolicy.ShouldSkipMemoryFallback(1, slots);
        Assert.True(actual);
    }

    [Fact]
    public void ShouldSkipMemoryFallback_NoFallbacks_False()
    {
        var slots = CulturePreference.Build(
            new CulturePreference.BuildOptions(
                Primary: "en-US",
                Second: "en-US",
                SecondEnabled: true,
                OsUiCulture: null,
                EnablePrimaryFallback: false
            )
        );
        var actual = SearchFallbackPolicy.ShouldSkipMemoryFallback(1, slots);
        Assert.False(actual);
    }
}
