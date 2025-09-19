 using PreludeLib.Tests.Preprocess;
using Xunit.Abstractions;

namespace PreludeLib.Tests.Categories;

public class WeaverCategoryTests(ITestOutputHelper output) : CategoryTestsBase(output, new WeaverPreprocessor(output))
{
    // empty
}