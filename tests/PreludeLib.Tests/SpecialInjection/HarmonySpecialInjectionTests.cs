using Xunit.Abstractions;

namespace PreludeLib.Tests.SpecialInjection;

public class HarmonySpecialInjectionTests(ITestOutputHelper output) : SpecialInjectionTestsBase(output)
{
    // empty
}