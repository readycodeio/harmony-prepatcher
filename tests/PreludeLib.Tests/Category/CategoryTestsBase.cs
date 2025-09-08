using Xunit.Abstractions;

namespace PreludeLib.Tests.Category;

public abstract class CategoryTestsBase(ITestOutputHelper output) : IsolatedBackendTestsBase(output)
{
    [Fact]
    public void PatchCategoryAppliesOnlySpecifiedCategories()
        => RunTestIsolated(nameof(PatchCategoryAppliesOnlySpecifiedCategories));

    [Fact]
    public void PatchAllUncategorizedAppliesOnlyUncategorizedPatches()
        => RunTestIsolated(nameof(PatchAllUncategorizedAppliesOnlyUncategorizedPatches));
}