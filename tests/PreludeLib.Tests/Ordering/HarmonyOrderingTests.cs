using Xunit.Abstractions;

namespace PreludeLib.Tests.Ordering;

[Collection("HarmonyNonParallel")]
public class HarmonyOrderingTests(ITestOutputHelper output) : OrderingTestsBase(output)
{
    // empty
}