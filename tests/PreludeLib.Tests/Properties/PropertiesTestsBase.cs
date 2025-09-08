using Xunit.Abstractions;

namespace PreludeLib.Tests.Properties;

public abstract class PropertiesTestsBase(ITestOutputHelper output) : IsolatedBackendTestsBase(output)
{
    [Fact]
    public void PatchPropertySetterWithMethodTypeSetter()
        => RunTestIsolated(nameof(PatchPropertySetterWithMethodTypeSetter));
    
    [Fact]
    public void PrefixOnAutoPropertySetterCanModifyIncomingValue()
        => RunTestIsolated(nameof(PrefixOnAutoPropertySetterCanModifyIncomingValue));
}