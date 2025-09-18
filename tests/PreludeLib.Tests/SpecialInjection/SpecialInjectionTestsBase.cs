using PreludeLib.Tests.Preprocess;
using Xunit.Abstractions;

namespace PreludeLib.Tests.SpecialInjection;

public abstract class SpecialInjectionTestsBase(ITestOutputHelper output, ITestPreprocessor? preprocessor = null) : IsolatedBackendTestsBase(output, preprocessor)
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
