using PreludeLib.Tests.Preprocess;
using Xunit.Abstractions;

namespace PreludeLib.Tests.Foreach;

public abstract class ForeachTestsBase(ITestOutputHelper output, ITestPreprocessor? preprocessor = null) : IsolatedBackendTestsBase(output, preprocessor)
{
    [Fact]
    public void PatchedMethodDoesNotThrowInvalidIlException()
        => RunTestIsolated(nameof(PatchedMethodDoesNotThrowInvalidIlException));
    
    [Fact]
    public void WorksWithNestedForeachLoops()
        => RunTestIsolated(nameof(WorksWithNestedForeachLoops));
}