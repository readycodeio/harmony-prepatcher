using PreludeLib.Tests.Preprocess;
using Xunit.Abstractions;

namespace PreludeLib.Tests.Prefix;

public class WeaverPrefixTests(ITestOutputHelper output) : PrefixTestsBase(output, new WeaverPreprocessor(output))
{
    // empty
}