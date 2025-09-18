using PreludeLib.Tests.Preprocess;
using Xunit.Abstractions;

namespace PreludeLib.Tests.Properties;

public class WeaverPropertiesTests(ITestOutputHelper output) : PropertiesTestsBase(output, new WeaverPreprocessor(output))
{
    // empty
}