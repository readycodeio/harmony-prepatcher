using PreludeLib.Tests.Preprocess;
using Xunit.Abstractions;

namespace PreludeLib.Tests.Prefix;

public abstract class PrefixTestsBase(ITestOutputHelper output, ITestPreprocessor? preprocessor = null) : IsolatedBackendTestsBase(output, preprocessor)
{
    [Fact]
    public void PrefixReturningFalseSkipsOriginalAndSetsResult()
        => RunTestIsolated(nameof(PrefixReturningFalseSkipsOriginalAndSetsResult));

    [Fact]
    public void PrefixCanModifyByRefArguments()
        => RunTestIsolated(nameof(PrefixCanModifyByRefArguments));
    
    [Fact]
    public void PrefixCanSetOutParameterValues()
        => RunTestIsolated(nameof(PrefixCanSetOutParameterValues));
    
    [Fact]
    public void PrefixCanUseArgumentIndexAliases__0__1()
        => RunTestIsolated(nameof(PrefixCanUseArgumentIndexAliases__0__1));
}