using PolicyPlusPlus.Services;
using Xunit;

namespace PolicyPlusModTests.Services
{
    public class BookmarkServiceTests
    {
        [Fact]
        public void Toggle_AddsAndRemoves()
        {
            var svc = BookmarkService.Instance; // singleton; test assumes clean or tolerant state
            var id = "TEST_POLICY_ID";
            // Ensure removed
            if (svc.IsBookmarked(id)) svc.Toggle(id);
            Assert.False(svc.IsBookmarked(id));
            svc.Toggle(id);
            Assert.True(svc.IsBookmarked(id));
            svc.Toggle(id);
            Assert.False(svc.IsBookmarked(id));
        }

        [Fact]
        public void ActiveIds_ReflectsToggle()
        {
            var svc = BookmarkService.Instance;
            var id = "TEST_POLICY_ID_2";
            if (svc.IsBookmarked(id)) svc.Toggle(id);
            Assert.DoesNotContain(id, svc.ActiveIds);
            svc.Toggle(id);
            Assert.Contains(id, svc.ActiveIds);
            svc.Toggle(id);
            Assert.DoesNotContain(id, svc.ActiveIds);
        }
    }
}
