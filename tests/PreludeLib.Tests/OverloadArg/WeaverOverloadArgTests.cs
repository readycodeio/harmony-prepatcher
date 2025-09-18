using PreludeLib.Tests.Preprocess;
using Xunit.Abstractions;

namespace PreludeLib.Tests.OverloadArg;

public class WeaverOverloadArgTests(ITestOutputHelper output) : OverloadArgTestsBase(output, new WeaverPreprocessor(output))
{
    // empty
}