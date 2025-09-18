using PreludeLib.Tests.Preprocess;
using Xunit.Abstractions;

namespace PreludeLib.Tests.Targeting;

public abstract class TargetingTestsBase(ITestOutputHelper output, ITestPreprocessor? preprocessor = null) : IsolatedBackendTestsBase(output, preprocessor)
{
    [Fact]
    public void PatchByMethodNameAndSignatureTargetsCorrectOverload()
        => RunTestIsolated(nameof(PatchByMethodNameAndSignatureTargetsCorrectOverload));

    [Fact]
    public void PatchPropertyGetterWithMethodTypeGetter()
        => RunTestIsolated(nameof(PatchPropertyGetterWithMethodTypeGetter));

    [Fact]
    public void PatchConstructorWithMethodTypeConstructor()
        => RunTestIsolated(nameof(PatchConstructorWithMethodTypeConstructor));
}