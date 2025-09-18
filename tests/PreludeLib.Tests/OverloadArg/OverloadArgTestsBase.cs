using PreludeLib.Tests.Preprocess;
using Xunit.Abstractions;

namespace PreludeLib.Tests.OverloadArg;

public abstract class OverloadArgTestsBase(ITestOutputHelper output, ITestPreprocessor? preprocessor = null) : IsolatedBackendTestsBase(output, preprocessor)
{
    [Fact]
    public void PatchOverloadWithByRefArgumentUsingArgumentTypeRef()
        => RunTestIsolated(nameof(PatchOverloadWithByRefArgumentUsingArgumentTypeRef));

    [Fact]
    public void PatchOverloadWithOutArgumentUsingArgumentTypeOut()
        => RunTestIsolated(nameof(PatchOverloadWithOutArgumentUsingArgumentTypeOut));
}