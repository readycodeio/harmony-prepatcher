 using PreludeLib.Tests.Preprocess;
using Xunit.Abstractions;

namespace PreludeLib.Tests.Category;

public class WeaverCategoryTests(ITestOutputHelper output) : CategoryTestsBase(output, new WeaverPreprocessor(output))
{
    // empty
}