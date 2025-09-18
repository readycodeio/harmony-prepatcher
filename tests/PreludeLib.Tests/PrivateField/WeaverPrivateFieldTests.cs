using PreludeLib.Tests.Preprocess;
using Xunit.Abstractions;

namespace PreludeLib.Tests.PrivateField;

public class WeaverPrivateFieldTests(ITestOutputHelper output) : PrivateFieldTestsBase(output, new WeaverPreprocessor(output))
{
    // empty
}