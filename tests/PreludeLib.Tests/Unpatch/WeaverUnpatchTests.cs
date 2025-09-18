using PreludeLib.Tests.Preprocess;
using Xunit.Abstractions;

namespace PreludeLib.Tests.Unpatch;

public class WeaverUnpatchTests(ITestOutputHelper output) : UnpatchTestsBase(output, new WeaverPreprocessor(output))
{
    // empty
}