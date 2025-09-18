using Xunit;

namespace PolicyPlusModTests.Testing
{
    [CollectionDefinition(Name)]
    public class UiTestCollection : ICollectionFixture<UiTestFixture>
    {
        public const string Name = "UI Tests";
    }

    public class UiTestFixture
    {
        // Placeholder for future shared UI initialization if needed
    }
}
