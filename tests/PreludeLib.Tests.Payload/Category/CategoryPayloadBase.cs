using Microsoft.Extensions.Logging;
using PreludeLib.Tests.Examples;
using PreludeLib.Tests.Patches.Category;
using Xunit;

namespace PreludeLib.Payload.Category;

public abstract class CategoryPayloadBase(bool shouldPass, ILogger logger) : BackendPayloadBase(shouldPass, logger)
{
    public void PatchCategoryAppliesOnlySpecifiedCategories()
    {
        var id = GenerateId(nameof(PatchCategoryAppliesOnlySpecifiedCategories));
        var builder = CreateBuilder(id);
        var owner = GetOrCreatePrelude();

        // Apply ONLY "alpha" category from THIS payload assembly
        builder.ScanAndPatchCategory(typeof(CategoryHelper).Assembly, "alpha");
        owner.Commit();

        try
        {
            var t = new CategoryTargets();

            // Start with 5:
            // - alpha adds +100
            // - beta NOT applied
            // - uncategorized NOT applied here
            int result = t.Op(5);
            Assert.Equal(5 + 100, result);
        }
        finally
        {
            builder.UnpatchAll();
            owner.Commit();
        }
    }

    public void PatchAllUncategorizedAppliesOnlyUncategorizedPatches()
    {
        var id = GenerateId(nameof(PatchAllUncategorizedAppliesOnlyUncategorizedPatches));
        var builder = CreateBuilder(id);
        var owner = GetOrCreatePrelude();

        // Apply ONLY patch classes WITHOUT a category from THIS payload assembly
        builder.ScanAndPatchUncategorized(typeof(CategoryHelper).Assembly);
        owner.Commit();

        try
        {
            var t = new CategoryTargets();

            // Start with 7:
            // - uncategorized adds +10
            // - alpha/beta NOT applied here
            int result = t.Op(7);
            Assert.Equal(7 + 10, result);
        }
        finally
        {
            builder.UnpatchAll();
            owner.Commit();
        }
    }
}