using PreludeLib.Tests.Preprocess;
using Xunit.Abstractions;

namespace PreludeLib.Tests.PropertyTargets;

public class WeaverPropertyTargetsTests(ITestOutputHelper output) : PropertyTargetsTestsBase(output, new WeaverPreprocessor(output))
{
    // empty
}