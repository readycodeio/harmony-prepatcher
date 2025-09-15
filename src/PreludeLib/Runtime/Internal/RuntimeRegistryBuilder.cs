using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using PreludeLib.Runtime.Registry;

namespace PreludeLib.Runtime.Internal;

public class RuntimeRegistryBuilder(string id, IRuntimePatchRegistry registry) : IRuntimeRegistryBuilder
{
    public string Id { get; } = id;
    
    public void ScanAndPatchAll(Assembly patchAssembly)
    {
        foreach (var type in GetAllTypes(patchAssembly))
        {
            ScanAndPatch(type);
        }
    }

    public void ScanAndPatchCategory(Assembly patchAssembly, string category)
    {
        foreach (var type in GetMatchingTypes(patchAssembly, category))
        {
            ScanAndPatch(type);
        }
    }

    public void ScanAndPatchUncategorized(Assembly patchAssembly)
    {
        foreach (var type in GetMatchingTypes(patchAssembly, category: null))
        {
            ScanAndPatch(type);
        }
    }

    public void ScanAndPatch(Type containerType)
    {
        var builder = new RuntimeContainerTypeRegistryBuilder(this, containerType);
        builder.Patch();
    }

    public void Patch(
        MethodBase original, 
        HarmonyMethod? prefix = null,
        HarmonyMethod? postfix = null,
        HarmonyMethod? finalizer = null,
        HarmonyMethod? transpiler = null)
    {
        if (prefix != null)
            Patch(original, HarmonyPatchType.Prefix, prefix);
        if (postfix != null)
            Patch(original, HarmonyPatchType.Postfix, postfix);
        if (finalizer != null)
            Patch(original, HarmonyPatchType.Finalizer, finalizer);
        if (transpiler != null)
            Patch(original, HarmonyPatchType.Transpiler, transpiler);
    }

    public void Patch(MethodBase original, HarmonyPatchType patchType, HarmonyMethod patchMethod)
    {
        registry.AddOriginalMethod(original);
        registry.AddPatchMethod(original, Id, patchType, patchMethod);
    }

    public void PatchPrefix(MethodBase original, HarmonyMethod prefix)
        => Patch(original, HarmonyPatchType.Prefix, prefix);

    public void PatchPostfix(MethodBase original, HarmonyMethod prefix)
        => Patch(original, HarmonyPatchType.Postfix, prefix);

    public void PatchFinalizer(MethodBase original, HarmonyMethod prefix)
        => Patch(original, HarmonyPatchType.Finalizer, prefix);

    public void UnpatchAll()
    {
        foreach (var original in registry.GetOriginalMethods()) // all originals
        {
            foreach (var patchMethod in GetOwnedPatch(original))
            {
                registry.RemovePatchMethod(original, Id, patchMethod);
            }
        }
    }

    public void UnpatchAll(Assembly patchAssembly)
    {
        foreach (var original in registry.GetOriginalMethods()) // all originals
        {
            foreach (var patchMethod in GetOwnedMatchingPatch(original, patchAssembly))
            {
                registry.RemovePatchMethod(original, Id, patchMethod);
            }
        }
    }

    public void UnpatchCategory(Assembly patchAssembly, string category)
    {
        foreach (var original in registry.GetOriginalMethods()) // all originals
        {
            foreach (var patchMethod in GetOwnedMatchingPatch(original, patchAssembly, category))
            {
                registry.RemovePatchMethod(original, Id, patchMethod);
            }
        }
    }

    public void UnpatchUncategorized(Assembly patchAssembly)
    {
        foreach (var original in registry.GetOriginalMethods()) // all originals
        {
            foreach (var patchMethod in GetOwnedMatchingPatch(original, category: null))
            {
                registry.RemovePatchMethod(original, Id, patchMethod);
            }
        }
    }

    public void UnpatchCategory(string category)
    {
        foreach (var original in registry.GetOriginalMethods()) // all originals
        {
            foreach (var patchMethod in GetOwnedMatchingPatch(original, category))
            {
                registry.RemovePatchMethod(original, Id, patchMethod);
            }
        }
    }

    public void UnpatchUncategorized()
    {
        foreach (var original in registry.GetOriginalMethods()) // all originals
        {
            foreach (var patchMethod in GetOwnedMatchingPatch(original, category: null))
            {
                registry.RemovePatchMethod(original, Id, patchMethod);
            }
        }
    }

    public void Unpatch(MethodBase original, HarmonyPatchType patchType)
    {
        registry.RemovePatchMethod(original, Id, patchType);
    }

    public void Unpatch(MethodBase original, MethodInfo patch)
    {
        registry.RemovePatchMethod(original, Id, patch);
    }
    
    // ---

    private IEnumerable<HarmonyMethod> GetOwnedPatch(MethodBase? original)
    {
        if (original == null)
            return [];

        if (!registry.HasOriginalMethod(original))
            return [];

        return registry.GetPatchMethods(original, Id, HarmonyPatchType.All).ToList();
    }
    
    private IEnumerable<HarmonyMethod> GetOwnedMatchingPatch(MethodBase? original, string? category)
        => GetOwnedPatch(original)
            .Where(x => IsPatchMatching(x, category));

    private IEnumerable<HarmonyMethod> GetOwnedMatchingPatch(MethodBase? original, Assembly patchAssembly)
        => GetOwnedPatch(original)
            .Where(x => IsPatchMatching(x, patchAssembly));

    private IEnumerable<HarmonyMethod> GetOwnedMatchingPatch(MethodBase? original, Assembly patchAssembly, string? category)
        => GetOwnedPatch(original)
            .Where(x => IsPatchMatching(x, patchAssembly, category));

    private static bool IsPatchMatching(HarmonyMethod patchMethod, string? category)
        => patchMethod.category == category;

    private static bool IsPatchMatching(HarmonyMethod patchMethod, Assembly patchAssembly)
        => patchMethod.method.Module.Assembly == patchAssembly;

    private static bool IsPatchMatching(HarmonyMethod patchMethod, Assembly patchAssembly, string? category)
        => IsPatchMatching(patchMethod, patchAssembly) && IsPatchMatching(patchMethod, category);
    
    // ---
    
    private readonly ConditionalWeakTable<Assembly, List<Type>> _allHarmonyPatchCache = new();
    private readonly ConditionalWeakTable<Assembly, Dictionary<string, List<Type>>> _categoryPatchCache = new();
    private readonly ConditionalWeakTable<Assembly, List<Type>> _uncategorizedPatchCache = new();

    private static string? GetCategory(Type type)
    {
        var harmonyAttributes = HarmonyMethodExtensions.GetFromType(type);
        if (harmonyAttributes.Count == 0) 
            return null;
        var containerAttributes = HarmonyMethod.Merge(harmonyAttributes);
        return containerAttributes.category;
    }

    private List<Type> GetAllTypes(Assembly patchAssembly)
    {
        if (_allHarmonyPatchCache.TryGetValue(patchAssembly, out var result))
            return result;
        
        result = [];
        foreach (var type in AccessTools.GetTypesFromAssembly(patchAssembly))
        {
            if (!type.HasHarmonyAttribute())
                continue;
            result.Add(type);
        }

        _allHarmonyPatchCache.Add(patchAssembly, result);
        return result;
    }
    
    private List<Type> GetMatchingTypes(Assembly patchAssembly, string? category)
    {
        List<Type>? result;
        
        if (category == null)
        {
            if (!_uncategorizedPatchCache.TryGetValue(patchAssembly, out result))
            {
                result = [];
                _uncategorizedPatchCache.Add(patchAssembly, result);
            }
        }
        else
        {
            if (!_categoryPatchCache.TryGetValue(patchAssembly, out var d))
            {
                d = [];
                _categoryPatchCache.Add(patchAssembly, d);
            }
            if (!d.TryGetValue(category, out result))
            {
                result = [];
                d.Add(category, result);
            }
            
            d.Add(category, result);
        }
        
        var allTypes = GetAllTypes(patchAssembly);
        result.AddRange(allTypes.Where(typeDef => GetCategory(typeDef) == category).ToList());
        return result;
    }
}