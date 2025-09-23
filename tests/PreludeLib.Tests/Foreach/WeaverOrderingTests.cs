using PreludeLib.Tests.Preprocess;
using Xunit.Abstractions;

namespace PreludeLib.Tests.Foreach;

public class WeaverForeachTests(ITestOutputHelper output) : ForeachTestsBase(output, new WeaverPreprocessor(output))
{
    // empty
}