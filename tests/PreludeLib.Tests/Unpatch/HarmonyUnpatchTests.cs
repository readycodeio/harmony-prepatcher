using Xunit.Abstractions;

namespace PreludeLib.Tests.Unpatch;

[Collection("HarmonyNonParallel")]
public class HarmonyUnpatchTests(ITestOutputHelper output) : UnpatchTestsBase(output)
{
    // empty
}