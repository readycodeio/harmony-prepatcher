using Xunit.Abstractions;

namespace PreludeLib.Tests.OverloadArg;

public abstract class OverloadArgTestsBase(ITestOutputHelper output) : IsolatedBackendTestsBase(output)
{
    [Fact]
    public void PatchOverloadWithByRefArgumentUsingArgumentTypeRef()
        => RunTestIsolated(nameof(PatchOverloadWithByRefArgumentUsingArgumentTypeRef));

    [Fact]
    public void PatchOverloadWithOutArgumentUsingArgumentTypeOut()
        => RunTestIsolated(nameof(PatchOverloadWithOutArgumentUsingArgumentTypeOut));
}