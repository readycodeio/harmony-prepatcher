using Xunit.Abstractions;

namespace PreludeLib.Tests.Postfix;

public abstract class PostfixTestsBase(ITestOutputHelper output) : IsolatedBackendTestsBase(output)
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