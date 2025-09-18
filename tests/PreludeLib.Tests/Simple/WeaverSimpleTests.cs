using PreludeLib.Tests.Preprocess;
using Xunit.Abstractions;

namespace PreludeLib.Tests.Simple;

public class WeaverSimpleTests(ITestOutputHelper output) : SimpleTestsBase(output, new WeaverPreprocessor(output))
{
    // empty
}