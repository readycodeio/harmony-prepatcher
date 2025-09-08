using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using Microsoft.Extensions.Logging;
using Xunit;

namespace PreludeLib.Runtime.HarmonyBackend;

public class PreludeHarmonyBackend(string id, ILogger logger) : IPreludeBackend
{
    public Harmony Harmony { get; } = new(id);

    public IPreludeClassProcessor CreateClassProcessor(Type type)
        => new PreludeHarmonyClassProcessor(this, type);

    public void PatchAll(Assembly patchAssembly)
        => Harmony.PatchAllUncategorized(patchAssembly);

    public void PatchCategory(Assembly patchAssembly, string category)
        => Harmony.PatchCategory(patchAssembly, category);

    public void PatchAllUncategorized(Assembly patchAssembly)
        => Harmony.PatchAllUncategorized(patchAssembly);

    public void Patch(
        MethodInfo original,
        PreludeMethod? prefix = null,
        PreludeMethod? postfix = null,
        PreludeMethod? finalizer = null,
        PreludeMethod? transpiler = null
    )
        => Harmony.Patch(
            original, 
            prefix: prefix?.ToHarmonyMethod(),
            postfix: postfix?.ToHarmonyMethod(),
            finalizer: finalizer?.ToHarmonyMethod(),
            transpiler: transpiler?.ToHarmonyMethod()
        );

    private void HarmonyUnpatch(MethodBase original, MethodInfo patch)
    {
        logger.LogInformation("Unpatching {0} from {1}", patch, original);
        Harmony.Unpatch(original, patch);
    }

    public void UnpatchAll()
    {
        foreach (var original in Harmony.GetAllPatchedMethods()) // all originals
        {
            var patches = Harmony.GetPatchInfo(original);
            if (patches == null)
                continue;

            foreach (var patch in GetOwnedPatch(original))
            {
                Assert.Equal(id, patch.owner);
                HarmonyUnpatch(original, patch.PatchMethod);
            }
        }
    }

    public void UnpatchAll(Assembly patchAssembly)
    {
        foreach (var original in Harmony.GetAllPatchedMethods()) // all originals
        {
            var patches = Harmony.GetPatchInfo(original);
            if (patches == null)
                continue;

            foreach (var patch in GetOwnedMatchingPatch(original, patchAssembly))
            {
                Assert.Equal(id, patch.owner);
                HarmonyUnpatch(original, patch.PatchMethod);
            }
        }
    }

    public void UnpatchCategory(Assembly patchAssembly, string category)
    {
        foreach (var original in Harmony.GetAllPatchedMethods()) // all originals
        {
            var patches = Harmony.GetPatchInfo(original);
            if (patches == null)
                continue;

            foreach (var patch in GetOwnedMatchingPatch(original, patchAssembly, category))
            {
                Assert.Equal(id, patch.owner);
                HarmonyUnpatch(original, patch.PatchMethod);
            }
        }
    }

    public void UnpatchUncategorized(Assembly patchAssembly)
    {
        foreach (var original in Harmony.GetAllPatchedMethods()) // all originals
        {
            var patches = Harmony.GetPatchInfo(original);
            if (patches == null)
                continue;

            foreach (var patch in GetOwnedMatchingPatch(original, category: null))
            {
                Assert.Equal(id, patch.owner);
                HarmonyUnpatch(original, patch.PatchMethod);
            }
        }
    }

    public void UnpatchCategory(string category)
    {
        foreach (var original in Harmony.GetAllPatchedMethods()) // all originals
        {
            var patches = Harmony.GetPatchInfo(original);
            if (patches == null)
                continue;

            foreach (var patch in GetOwnedMatchingPatch(original, category))
            {
                Assert.Equal(id, patch.owner);
                HarmonyUnpatch(original, patch.PatchMethod);
            }
        }
    }

    public void UnpatchUncategorized()
    {
        foreach (var original in Harmony.GetAllPatchedMethods()) // all originals
        {
            var patches = Harmony.GetPatchInfo(original);
            if (patches == null)
                continue;

            foreach (var patch in GetOwnedMatchingPatch(original, category: null))
            {
                Assert.Equal(id, patch.owner);
                HarmonyUnpatch(original, patch.PatchMethod);
            }
        }
    }

    public void Unpatch(MethodBase original, HarmonyPatchType patchType)
        => Harmony.Unpatch(original, patchType, id);

    public void Unpatch(MethodBase original, MethodInfo patch)
    {
        EnsurePatchOwned(original, patch, out var patchInfo);
        Assert.Equal(id, patchInfo.owner);
        HarmonyUnpatch(original, patch);
    }

    // ---
    
    private bool GetAnyPatchInfo(MethodBase original, MethodInfo patch, [NotNullWhen(true)] out Patch? patchInfo)
    {
        var patches = Harmony.GetPatchInfo(original);
        patchInfo = patches.Prefixes.FirstOrDefault(x => x.PatchMethod == patch);
        patchInfo ??= patches.Postfixes.FirstOrDefault(x => x.PatchMethod == patch);
        patchInfo ??= patches.Finalizers.FirstOrDefault(x => x.PatchMethod == patch);
        patchInfo ??= patches.Transpilers.FirstOrDefault(x => x.PatchMethod == patch);
        return patchInfo != null;
    }

    private bool IsOwnedPatch(MethodBase original, MethodInfo patch, [NotNullWhen(true)] out Patch? patchInfo)
    {
        if (!GetAnyPatchInfo(original, patch, out patchInfo))
            return false;
        return patchInfo.owner == id;
    }
    
    private void EnsurePatchOwned(MethodBase original, MethodInfo patch, out Patch patchInfo)
    {
        if (!GetAnyPatchInfo(original, patch, out patchInfo))
        {
            throw new ArgumentException("The specified patch was not applied.", nameof(patch));
        }
        
        if (patchInfo.owner != id)
        {
            throw new ArgumentException($"The specified patch method is not applied to the original method, actual owner: {patchInfo.owner}.", nameof(patch));
        }
    }

    private IEnumerable<Patch> GetOwnedPatch(MethodBase? original)
    {
        var patches = Harmony.GetPatchInfo(original);
        return patches.Prefixes.Where(x => x.owner == id)
            .Concat(patches.Postfixes.Where(x => x.owner == id))
            .Concat(patches.Finalizers.Where(x => x.owner == id))
            .Concat(patches.Transpilers.Where(x => x.owner == id));
    }

    private IEnumerable<Patch> GetOwnedMatchingPatch(MethodBase? original, string? category)
        => GetOwnedPatch(original)
            .Where(x => IsPatchMatching(x.PatchMethod, category));

    private IEnumerable<Patch> GetOwnedMatchingPatch(MethodBase? original, Assembly patchAssembly)
        => GetOwnedPatch(original)
            .Where(x => IsPatchMatching(x.PatchMethod, patchAssembly));

    private IEnumerable<Patch> GetOwnedMatchingPatch(MethodBase? original, Assembly patchAssembly, string? category)
        => GetOwnedPatch(original)
            .Where(x => IsPatchMatching(x.PatchMethod, patchAssembly, category));

    private static bool IsPatchMatching(MethodInfo patch, string? category)
    {
        if (category == null)
            return patch.DeclaringType?.GetCustomAttributes(typeof(HarmonyPatchCategory), inherit: false)
                .Any() == false;
        else
            return patch.DeclaringType?.GetCustomAttributes(typeof(HarmonyPatchCategory), inherit: false)
                .Cast<HarmonyPatchCategory>()
                .Any(attr => attr.info.category == category) == true;
    }

    private static bool IsPatchMatching(MethodInfo patch, Assembly patchAssembly)
        => patch.Module.Assembly == patchAssembly;

    private static bool IsPatchMatching(MethodInfo patch, Assembly patchAssembly, string? category)
        => IsPatchMatching(patch, patchAssembly) && IsPatchMatching(patch, category);
}