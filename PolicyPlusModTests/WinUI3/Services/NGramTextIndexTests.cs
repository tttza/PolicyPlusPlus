using System.Collections.Generic;
using PolicyPlusPlus.Services;
using Xunit;

namespace PolicyPlusModTests.WinUI3.Services
{
    public class NGramTextIndexTests
    {
        [Fact(DisplayName = "NGramTextIndex 2-gram builds and queries intersection")]
        public void BuildAndQuery_TwoGram()
        {
            var idx = new NGramTextIndex(2);
            var items = new List<(string id, string normalizedText)>
            {
                ("A", "enable dummy feature"),
                ("B", "policy settings page"),
            };
            idx.Build(items);
            var res = idx.TryQuery("dummy");
            Assert.NotNull(res);
            Assert.Contains("A", res!);
            Assert.DoesNotContain("B", res!);
        }

        [Fact(DisplayName = "NGramTextIndex returns null for too-short queries")]
        public void TryQuery_TooShort_ReturnsNull()
        {
            var idx = new NGramTextIndex(2);
            idx.Build(new[] { ("A", "ab") });
            Assert.Null(idx.TryQuery("a"));
        }
    }
}
