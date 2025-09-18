using PreludeLib.Tests.Preprocess;
using Xunit.Abstractions;

namespace PreludeLib.Tests.Finalizer;

public abstract class FinalizerTestsBase(ITestOutputHelper output, ITestPreprocessor? preprocessor = null) : IsolatedBackendTestsBase(output, preprocessor)
{
    [Fact]
    public void FinalizerReceivesExceptionWhenOriginalThrows()
        => RunTestIsolated(nameof(FinalizerReceivesExceptionWhenOriginalThrows));
    
    [Fact]
    public void FinalizerCanSuppressExceptionByReturningNull()
        => RunTestIsolated(nameof(FinalizerCanSuppressExceptionByReturningNull));
    
    [Fact]
    public void FinalizerRunsOnSuccessfulExecutionAndSeesNullException()
        => RunTestIsolated(nameof(FinalizerRunsOnSuccessfulExecutionAndSeesNullException));
}