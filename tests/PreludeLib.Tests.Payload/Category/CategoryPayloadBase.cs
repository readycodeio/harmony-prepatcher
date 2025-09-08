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
        var backend = CreateBackend(id);

        // Apply ONLY "alpha" category from THIS payload assembly
        backend.PatchCategory(typeof(CategoryHelper).Assembly, "alpha");

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
            backend.UnpatchAll();
        }
    }

    public void PatchAllUncategorizedAppliesOnlyUncategorizedPatches()
    {
        var id = GenerateId(nameof(PatchAllUncategorizedAppliesOnlyUncategorizedPatches));
        var harmony = CreateBackend(id);

        // Apply ONLY patch classes WITHOUT a category from THIS payload assembly
        harmony.PatchAllUncategorized(typeof(CategoryHelper).Assembly);

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
            harmony.UnpatchAll();
        }
    }
}