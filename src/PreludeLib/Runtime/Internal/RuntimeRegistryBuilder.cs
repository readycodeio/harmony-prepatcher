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

    public void ScanAndPatchCategory(Assembly patchAssembly, string? category)
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
        PatchTarget target, 
        HarmonyMethod? prefix = null,
        HarmonyMethod? postfix = null,
        HarmonyMethod? finalizer = null,
        HarmonyMethod? transpiler = null, 
        PatchGroup group = default)
    {
        if (prefix != null)
            Patch(target, HarmonyPatchType.Prefix, prefix, group);
        if (postfix != null)
            Patch(target, HarmonyPatchType.Postfix, postfix, group);
        if (finalizer != null)
            Patch(target, HarmonyPatchType.Finalizer, finalizer, group);
        if (transpiler != null)
            Patch(target, HarmonyPatchType.Transpiler, transpiler, group);
    }

    public void Patch(PatchTarget target, HarmonyPatchType patchType, HarmonyMethod patchMethod, PatchGroup group = default)
    {
        registry.AddGroup(group);
        registry.AddTarget(group, target);
        registry.AddPatchMethod(target, Id, patchType, patchMethod);
    }

    public void PatchPrefix(PatchTarget target, HarmonyMethod prefix, PatchGroup group = default)
        => Patch(target, HarmonyPatchType.Prefix, prefix, group);

    public void PatchPostfix(PatchTarget target, HarmonyMethod prefix, PatchGroup group = default)
        => Patch(target, HarmonyPatchType.Postfix, prefix, group);

    public void PatchFinalizer(PatchTarget target, HarmonyMethod prefix, PatchGroup group = default)
        => Patch(target, HarmonyPatchType.Finalizer, prefix, group);

    public void UnpatchAll()
    {
        foreach (var group in registry.GetGroups())
        {
            foreach (var target in registry.GetTargets(group)) // all originals
            {
                foreach (var patchMethod in GetOwnedPatch(target, group))
                {
                    registry.RemovePatchMethod(target, Id, patchMethod);
                }
            }
        }
    }

    public void UnpatchAll(Assembly patchAssembly)
    {
        foreach (var group in registry.GetGroups())
        {
            foreach (var target in registry.GetTargets(group))
            {
                foreach (var patchMethod in GetOwnedMatchingPatch(target, group, patchAssembly))
                {
                    registry.RemovePatchMethod(target, Id, patchMethod);
                }
            }
        }
    }

    public void UnpatchCategory(Assembly patchAssembly, string category)
    {
        foreach (var group in registry.GetGroups())
        {
            foreach (var target in registry.GetTargets(group))
            {
                foreach (var patchMethod in GetOwnedMatchingPatch(target, group, patchAssembly, category))
                {
                    registry.RemovePatchMethod(target, Id, patchMethod);
                }
            }
        }
    }

    public void UnpatchUncategorized(Assembly patchAssembly)
    {
        foreach (var group in registry.GetGroups())
        {
            foreach (var target in registry.GetTargets(group))
            {
                foreach (var patchMethod in GetOwnedMatchingPatch(target, group, category: null))
                {
                    registry.RemovePatchMethod(target, Id, patchMethod);
                }
            }
        }
    }

    public void UnpatchCategory(string category)
    {
        foreach (var group in registry.GetGroups())
        {
            foreach (var target in registry.GetTargets(group))
            {
                foreach (var patchMethod in GetOwnedMatchingPatch(target, group, category))
                {
                    registry.RemovePatchMethod(target, Id, patchMethod);
                }
            }
        }
    }

    public void UnpatchUncategorized()
    {
        foreach (var group in registry.GetGroups())
        {
            foreach (var target in registry.GetTargets(group))
            {
                foreach (var patchMethod in GetOwnedMatchingPatch(target, group, category: null))
                {
                    registry.RemovePatchMethod(target, Id, patchMethod);
                }
            }
        }
    }

    public void Unpatch(PatchTarget target, HarmonyPatchType patchType)
    {
        registry.RemovePatchMethod(target, Id, patchType);
    }

    public void Unpatch(PatchTarget target, MethodInfo patch)
    {
        registry.RemovePatchMethod(target, Id, patch);
    }
    
    // ---

    private IEnumerable<HarmonyMethod> GetOwnedPatch(PatchTarget target, PatchGroup group)
    {
        if (!registry.HasTarget(group, target))
            return [];

        return registry.GetPatchMethods(target, Id, HarmonyPatchType.All).ToList();
    }
    
    private IEnumerable<HarmonyMethod> GetOwnedMatchingPatch(PatchTarget target, PatchGroup group, string? category)
        => GetOwnedPatch(target, group)
            .Where(x => IsPatchMatching(x, category));

    private IEnumerable<HarmonyMethod> GetOwnedMatchingPatch(PatchTarget target, PatchGroup group, Assembly patchAssembly)
        => GetOwnedPatch(target, group)
            .Where(x => IsPatchMatching(x, patchAssembly));

    private IEnumerable<HarmonyMethod> GetOwnedMatchingPatch(PatchTarget target, PatchGroup group, Assembly patchAssembly, string? category)
        => GetOwnedPatch(target, group)
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
        }
        
        var allTypes = GetAllTypes(patchAssembly);
        result.AddRange(allTypes.Where(typeDef => GetCategory(typeDef) == category).ToList());
        return result;
    }
}