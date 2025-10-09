using PreludeLib.Tests.Preprocess;
using Xunit.Abstractions;

namespace PreludeLib.Tests.Categories;

public abstract class CategoryTestsBase(ITestOutputHelper output, ITestPreprocessor? preprocessor = null) : IsolatedBackendTestsBase(output, preprocessor)
{
    [Fact]
    public void PatchCategoryAppliesOnlySpecifiedCategories()
        => RunTestIsolated(nameof(PatchCategoryAppliesOnlySpecifiedCategories));

    [Fact]
    public void PatchAllUncategorizedAppliesOnlyUncategorizedPatches()
        => RunTestIsolated(nameof(PatchAllUncategorizedAppliesOnlyUncategorizedPatches));
    
    [Fact]
    public void PatchWithoutHarmonyPatchWorks()
        => RunTestIsolated(nameof(PatchWithoutHarmonyPatchWorks));
}