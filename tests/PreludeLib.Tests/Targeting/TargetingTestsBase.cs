using Xunit.Abstractions;

namespace PreludeLib.Tests.Targeting;

public abstract class TargetingTestsBase(ITestOutputHelper output) : IsolatedBackendTestsBase(output)
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