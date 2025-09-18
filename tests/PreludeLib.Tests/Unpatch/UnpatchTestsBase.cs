using PreludeLib.Tests.Preprocess;
using Xunit.Abstractions;

namespace PreludeLib.Tests.Unpatch;

[Collection("HarmonyNonParallel")]
public abstract class UnpatchTestsBase(ITestOutputHelper output, ITestPreprocessor? preprocessor = null) : IsolatedBackendTestsBase(output, preprocessor)
{
    [Fact]
    public void UnpatchSpecificPrefix_MiddleRemoval_KeepsOrderStable()
        => RunTestIsolated(nameof(UnpatchSpecificPrefix_MiddleRemoval_KeepsOrderStable));

    [Fact]
    public void UnpatchAll_ByOwnerId_RemovesOnlyThatOwnersPatches()
        => RunTestIsolated(nameof(UnpatchAll_ByOwnerId_RemovesOnlyThatOwnersPatches));

    [Fact]
    public void UnpatchSpecificPostfix_LeavesPrefixAndFinalizerActive()
        => RunTestIsolated(nameof(UnpatchSpecificPostfix_LeavesPrefixAndFinalizerActive));
}