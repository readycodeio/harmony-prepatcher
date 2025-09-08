using Xunit.Abstractions;

namespace PreludeLib.Tests.SpecialInjection;

public abstract class SpecialInjectionTestsBase(ITestOutputHelper output) : IsolatedBackendTestsBase(output)
{
    [Fact]
    public void Injected__instanceProvidesOriginalInstance()
        => RunTestIsolated(nameof(Injected__instanceProvidesOriginalInstance));

    [Fact]
    public void Injected__argsArrayCanMutateArgumentsInPlace()
        => RunTestIsolated(nameof(Injected__argsArrayCanMutateArgumentsInPlace));

    [Fact]
    public void Injected__originalMethodProvidesMethodBase()
        => RunTestIsolated(nameof(Injected__originalMethodProvidesMethodBase));

    [Fact]
    public void HarmonyArgumentAttributeBindsByIndexAndName()
        => RunTestIsolated(nameof(HarmonyArgumentAttributeBindsByIndexAndName));
}
