using PreludeLib.Tests.Preprocess;
using Xunit.Abstractions;

namespace PreludeLib.Tests.SpecialInjection;

public class WeaverSpecialInjectionTests(ITestOutputHelper output) : SpecialInjectionTestsBase(output, new WeaverPreprocessor(output))
{
    // empty
}