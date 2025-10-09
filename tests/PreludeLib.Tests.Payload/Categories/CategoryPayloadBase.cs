using Microsoft.Extensions.Logging;
using PreludeLib.Common;
using PreludeLib.Tests.Examples;
using PreludeLib.Tests.Patches.Categories;
using Xunit;
using Xunit.Sdk;

namespace PreludeLib.Tests.Payload.Categories;

public abstract class CategoryPayloadBase(bool shouldPass, ILogger logger) : BackendPayloadBase(shouldPass, logger)
{
    public void PatchCategoryAppliesOnlySpecifiedCategories()
    {
        var id = GenerateId(nameof(PatchCategoryAppliesOnlySpecifiedCategories));
        var builder = CreateBuilder(id);
        var owner = GetOrCreatePrelude();

        // Apply ONLY "alpha" category from THIS payload assembly
        builder.ScanAndPatchCategory(typeof(CategoryHelper).Assembly, new Common.Category("alpha"));
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

    public void PatchWithoutHarmonyPatchWorks()
    {
        var id = GenerateId(nameof(PatchWithoutHarmonyPatchWorks));
        var builder = CreateBuilder(id);
        var owner = GetOrCreatePrelude();

        var t = new OtherCategoryTargets();

        Assert.Throws<InvalidOperationException>(() => t.OtherMethod(7));
        
        builder.ScanAndPatchCategory(typeof(CategoryHelper).Assembly, new Category("noHarmonyPatch"));
        owner.Commit();

        try
        {
            int result = -1;
            try
            {
                Logger.LogInformation("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
                result = t.OtherMethod(7);
            }
            catch (System.Exception ex)
            {
                throw new XunitException("Should not throw", ex);
            }
            
            Assert.Equal(7 * 7, result);
        }
        finally
        {
            builder.UnpatchAll();
            owner.Commit();
        }
    }
}