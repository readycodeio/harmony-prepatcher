using Xunit.Abstractions;

namespace PreludeLib.Tests.Ordering;

[Collection("HarmonyNonParallel")]
public abstract class OrderingTestsBase(ITestOutputHelper output) : IsolatedBackendTestsBase(output)
{
    [Fact]
    public void IncrementalPrefixes_RespectPriorityAtEachStep()
        => RunTestIsolated(nameof(IncrementalPrefixes_RespectPriorityAtEachStep));

    [Fact]
    public void RegistrationOrderIrrelevant_PriorityDefinesOrder()
        => RunTestIsolated(nameof(RegistrationOrderIrrelevant_PriorityDefinesOrder));

    [Fact]
    public void CrossOwnerConstraints_EnforceZThenYThenX()
        => RunTestIsolated(nameof(CrossOwnerConstraints_EnforceZThenYThenX));

    [Fact]
    public void PostfixOrdering_RespectsBeforeAfterConstraints()
        => RunTestIsolated(nameof(PostfixOrdering_RespectsBeforeAfterConstraints));

    [Fact]
    public void FinalizerRunsAfterPostfixes()
        => RunTestIsolated(nameof(FinalizerRunsAfterPostfixes));

    [Fact]
    public void IncrementalAdd_WithCrossOwnerConstraints_StableFinalOrder()
        => RunTestIsolated(nameof(IncrementalAdd_WithCrossOwnerConstraints_StableFinalOrder));
}