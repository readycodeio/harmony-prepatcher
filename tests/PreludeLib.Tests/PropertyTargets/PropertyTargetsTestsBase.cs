using PreludeLib.Tests.Preprocess;
using Xunit.Abstractions;

namespace PreludeLib.Tests.PropertyTargets;

public abstract class PropertyTargetsTestsBase(ITestOutputHelper output, ITestPreprocessor? preprocessor = null) : IsolatedBackendTestsBase(output, preprocessor)
{
    [Fact]
    public void PatchPropertySetterWithMethodTypeSetter()
        => RunTestIsolated(nameof(PatchPropertySetterWithMethodTypeSetter));
    
    [Fact]
    public void PrefixOnAutoPropertySetterCanModifyIncomingValue()
        => RunTestIsolated(nameof(PrefixOnAutoPropertySetterCanModifyIncomingValue));
}