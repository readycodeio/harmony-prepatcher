using Xunit.Abstractions;

namespace PreludeLib.Tests.SpecialInjection;

public class DummySpecialInjectionTests(ITestOutputHelper output) : SpecialInjectionTestsBase(output)
{
    // empty
}