using PreludeLib.Tests.Preprocess;
using Xunit.Abstractions;

namespace PreludeLib.Tests.Ordering;

public class WeaverOrderingTests(ITestOutputHelper output) : OrderingTestsBase(output, new WeaverPreprocessor(output))
{
    // empty
}