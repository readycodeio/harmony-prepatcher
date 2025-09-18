using PreludeLib.Tests.Preprocess;
using Xunit.Abstractions;

namespace PreludeLib.Tests.PrivateField;

public abstract class PrivateFieldTestsBase(ITestOutputHelper output, ITestPreprocessor? preprocessor = null) : IsolatedBackendTestsBase(output, preprocessor)
{
    [Fact]
    public void PrefixCanReadPrivateFieldViaTripleUnderscore()
        => RunTestIsolated(nameof(PrefixCanReadPrivateFieldViaTripleUnderscore));
    
    [Fact]
    public void PrefixCanModifyPrivateFieldViaRefTripleUnderscore()
        => RunTestIsolated(nameof(PrefixCanModifyPrivateFieldViaRefTripleUnderscore));

    [Fact]
    public void PostfixCanObservePrivateFieldChangesFromPrefix()
        => RunTestIsolated(nameof(PostfixCanObservePrivateFieldChangesFromPrefix));
}