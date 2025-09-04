using Xunit;

namespace HarmonyWeaver.Tests
{
    /// <summary>
    /// Test collection to ensure integration tests don't run in parallel with each other
    /// This prevents conflicts with shared static state and file system resources
    /// </summary>
    [CollectionDefinition("IntegrationTests")]
    public class IntegrationTestCollection : ICollectionFixture<IntegrationTestFixture>
    {
        // This class has no code, and is never instantiated. Its purpose is simply
        // to be the place to apply [CollectionDefinition] and all the
        // ICollectionFixture<> interfaces.
    }

    /// <summary>
    /// Shared fixture for integration tests
    /// </summary>
    public class IntegrationTestFixture : IDisposable
    {
        public IntegrationTestFixture()
        {
            // Any shared setup can go here
        }

        public void Dispose()
        {
            // Clean up any shared resources
            HarmonyWeaver.Core.Logging.LoggerProvider.ClearAllLoggers();
        }
    }
}