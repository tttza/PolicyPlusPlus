using Xunit;

namespace PolicyPlusModTests.TestHelpers;

// xUnit collection definition that binds the IsolatedCacheFixture lifecycle to all tests tagged with [Collection("AdmxCache.Isolated")]
[CollectionDefinition("AdmxCache.Isolated")]
public class AdmxCacheIsolatedCollection : ICollectionFixture<IsolatedCacheFixture>
{
    // Intentionally empty - only used for collection+fixture binding
}
