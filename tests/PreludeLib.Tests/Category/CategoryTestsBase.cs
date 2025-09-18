using PreludeLib.Tests.Preprocess;
using Xunit.Abstractions;

namespace PreludeLib.Tests.Category;

public abstract class CategoryTestsBase(ITestOutputHelper output, ITestPreprocessor? preprocessor = null) : IsolatedBackendTestsBase(output, preprocessor)
{
    [Fact]
    public void PatchCategoryAppliesOnlySpecifiedCategories()
        => RunTestIsolated(nameof(PatchCategoryAppliesOnlySpecifiedCategories));

    [Fact]
    public void PatchAllUncategorizedAppliesOnlyUncategorizedPatches()
        => RunTestIsolated(nameof(PatchAllUncategorizedAppliesOnlyUncategorizedPatches));
}