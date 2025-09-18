using PreludeLib.Tests.Preprocess;
using Xunit.Abstractions;

namespace PreludeLib.Tests.Postfix;

public abstract class PostfixTestsBase(ITestOutputHelper output, ITestPreprocessor? preprocessor = null) : IsolatedBackendTestsBase(output, preprocessor)
{
    [Fact]
    public void PostfixCanReadAndModify__result()
        => RunTestIsolated(nameof(PostfixCanReadAndModify__result));

    [Fact]
    public void PostfixOnVoidMethodExecutes()
        => RunTestIsolated(nameof(PostfixOnVoidMethodExecutes));

    [Fact]
    public void PostfixReceivesStateFromPrefixVia__state()
        => RunTestIsolated(nameof(PostfixReceivesStateFromPrefixVia__state));
    
    [Fact]
    public void PostfixSeesArgsAfterPrefixModifications()
        => RunTestIsolated(nameof(PostfixSeesArgsAfterPrefixModifications));
}