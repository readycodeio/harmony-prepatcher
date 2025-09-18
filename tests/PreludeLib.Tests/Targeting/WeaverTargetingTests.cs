using PreludeLib.Tests.Preprocess;
using Xunit.Abstractions;

namespace PreludeLib.Tests.Targeting;

public class WeaverTargetingTests(ITestOutputHelper output) : TargetingTestsBase(output, new WeaverPreprocessor(output))
{
    // empty
}