using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Microsoft.Extensions.Logging;
using PreludeLib.Common;
using PreludeLib.Runtime.Public;
using PreludeLib.Runtime.Registry;

namespace PreludeLib.Runtime.Internal;

public class RuntimeRegistryBuilder : IRuntimeRegistryBuilder
{
    public string Id { get; }

    public void ScanAndPatchAll()
    {
        foreach (var asm in GetAllPatchAssemblies())
        {
            ScanAndPatchAll(asm);
        }
    }

    public void ScanAndPatchAll(Assembly patchAssembly)
    {
        foreach (var type in GetAllTypes(patchAssembly))
        {
            ScanAndPatch(type);
        }
    }

    public void ScanAndPatchAllCalling()
    {
        var callingAsm = Assembly.GetCallingAssembly();
        ScanAndPatchAll(callingAsm);
    }

    public void ScanAndPatchCategory(Category category)
    {
        foreach (var asm in GetAllPatchAssemblies())
        {
            ScanAndPatchCategory(asm, category);
        }
    }

    public void ScanAndPatchCategory(Assembly patchAssembly, Category category)
    {
        foreach (var type in GetMatchingTypes(patchAssembly, category))
        {
            ScanAndPatch(type);
        }
    }

    public void ScanAndPatchCategoryCalling(Category category)
    {
        var callingAsm = Assembly.GetCallingAssembly();
        ScanAndPatchCategory(callingAsm, category);
    }

    public void ScanAndPatchUncategorized()
    {
        foreach (var asm in GetAllPatchAssemblies())
        {
            ScanAndPatchUncategorized(asm);
        }
    }

    public void ScanAndPatchUncategorized(Assembly patchAssembly)
    {
        foreach (var type in GetMatchingTypes(patchAssembly, category: Category.Uncategorized))
        {
            ScanAndPatch(type);
        }
    }

    public void ScanAndPatchUncategorizedCalling()
    {
        var  callingAsm = Assembly.GetCallingAssembly();
        ScanAndPatchUncategorized(callingAsm);
    }

    public void ScanAndPatch(Type containerType)
    {
        var builder = new RuntimeContainerTypeRegistryBuilder(this, containerType, _logger);
        builder.Patch();
    }
    
    public void Patch(
        PatchTarget target, 
        HarmonyMethod? prefix = null,
        HarmonyMethod? postfix = null,
        HarmonyMethod? finalizer = null,
        HarmonyMethod? transpiler = null)
    {
        if (prefix != null)
            Patch(target, HarmonyPatchType.Prefix, prefix);
        if (postfix != null)
            Patch(target, HarmonyPatchType.Postfix, postfix);
        if (finalizer != null)
            Patch(target, HarmonyPatchType.Finalizer, finalizer);
        if (transpiler != null)
            Patch(target, HarmonyPatchType.Transpiler, transpiler);
    }

    public void Patch(
        MethodBase original,
        HarmonyMethod? prefix = null,
        HarmonyMethod? postfix = null,
        HarmonyMethod? finalizer = null,
        HarmonyMethod? transpiler = null,
        PatchGroup group = default)
        => Patch(PatchTarget.FromOriginal(original, group), prefix, postfix, finalizer, transpiler);

    public void Patch(PatchTarget target, HarmonyPatchType patchType, HarmonyMethod patchMethod)
    {
        _registry.AddGroup(target.Group, Id);
        _registry.AddTarget(target, Id);
        _registry.AddPatchMethod(target, Id, patchType, patchMethod);
    }

    public void Patch(MethodBase original, HarmonyPatchType patchType, HarmonyMethod patchMethod, PatchGroup group = default)
        => Patch(PatchTarget.FromOriginal(original, group), patchType, patchMethod);

    public void PatchPrefix(PatchTarget target, HarmonyMethod prefix)
        => Patch(target, HarmonyPatchType.Prefix, prefix);

    public void PatchPrefix(MethodBase original, HarmonyMethod prefix, PatchGroup group = default)
        => Patch(PatchTarget.FromOriginal(original, group), HarmonyPatchType.Prefix, prefix);

    public void PatchPostfix(PatchTarget target, HarmonyMethod postfix)
        => Patch(target, HarmonyPatchType.Postfix, postfix);

    public void PatchPostfix(MethodBase original, HarmonyMethod postfix, PatchGroup group = default)
        => Patch(PatchTarget.FromOriginal(original, group), HarmonyPatchType.Postfix, postfix);

    public void PatchFinalizer(PatchTarget target, HarmonyMethod finalizer)
        => Patch(target, HarmonyPatchType.Finalizer, finalizer);

    public void PatchFinalizer(MethodBase original, HarmonyMethod finalizer, PatchGroup group = default)
        => Patch(PatchTarget.FromOriginal(original, group), HarmonyPatchType.Finalizer, finalizer);

    public void UnpatchAll()
    {
        foreach (var group in _registry.GetGroups(Id))
        {
            foreach (var target in _registry.GetTargets(group, Id))
            {
                foreach (var patchMethod in GetOwnedPatch(target, group))
                {
                    _registry.RemovePatchMethod(target, Id, patchMethod);
                }
            }
        }
    }

    public void UnpatchAll(Assembly patchAssembly)
    {
        foreach (var group in _registry.GetGroups(Id))
        {
            foreach (var target in _registry.GetTargets(group, Id))
            {
                foreach (var patchMethod in GetOwnedMatchingPatch(target, group, patchAssembly))
                {
                    _registry.RemovePatchMethod(target, Id, patchMethod);
                }
            }
        }
    }

    public void UnpatchCategory(Assembly patchAssembly, Category category)
    {
        foreach (var group in _registry.GetGroups(Id))
        {
            foreach (var target in _registry.GetTargets(group, Id))
            {
                foreach (var patchMethod in GetOwnedMatchingPatch(target, group, patchAssembly, category))
                {
                    _registry.RemovePatchMethod(target, Id, patchMethod);
                }
            }
        }
    }

    public void UnpatchUncategorized(Assembly patchAssembly)
    {
        foreach (var group in _registry.GetGroups(Id))
        {
            foreach (var target in _registry.GetTargets(group, Id))
            {
                foreach (var patchMethod in GetOwnedMatchingPatch(target, group, category: Category.Uncategorized))
                {
                    _registry.RemovePatchMethod(target, Id, patchMethod);
                }
            }
        }
    }

    public void UnpatchCategory(Category category)
    {
        foreach (var group in _registry.GetGroups(Id))
        {
            foreach (var target in _registry.GetTargets(group, Id))
            {
                foreach (var patchMethod in GetOwnedMatchingPatch(target, group, category))
                {
                    _registry.RemovePatchMethod(target, Id, patchMethod);
                }
            }
        }
    }

    public void UnpatchUncategorized()
    {
        foreach (var group in _registry.GetGroups(Id))
        {
            foreach (var target in _registry.GetTargets(group, Id))
            {
                foreach (var patchMethod in GetOwnedMatchingPatch(target, group, category: Category.Uncategorized))
                {
                    _registry.RemovePatchMethod(target, Id, patchMethod);
                }
            }
        }
    }

    public void Unpatch(PatchTarget target, HarmonyPatchType patchType)
    {
        _registry.RemovePatchMethod(target, Id, patchType);
    }

    public void Unpatch(MethodBase original, HarmonyPatchType patchType, PatchGroup group = default)
        => Unpatch(PatchTarget.FromOriginal(original, group), patchType);

    public void Unpatch(PatchTarget target, MethodInfo patch)
    {
        _registry.RemovePatchMethod(target, Id, patch);
    }

    public void Unpatch(MethodBase original, MethodInfo patch, PatchGroup group = default)
        => Unpatch(PatchTarget.FromOriginal(original, group), patch);

    // ---

    private IEnumerable<HarmonyMethod> GetOwnedPatch(PatchTarget target, PatchGroup group)
    {
        if (!_registry.HasTarget(target, Id))
            return [];

        return _registry.GetPatchMethods(target, Id, HarmonyPatchType.All).ToList();
    }
    
    private IEnumerable<HarmonyMethod> GetOwnedMatchingPatch(PatchTarget target, PatchGroup group, Category category)
        => GetOwnedPatch(target, group)
            .Where(x => IsPatchMatching(x, category));

    private IEnumerable<HarmonyMethod> GetOwnedMatchingPatch(PatchTarget target, PatchGroup group, Assembly patchAssembly)
        => GetOwnedPatch(target, group)
            .Where(x => IsPatchMatching(x, patchAssembly));

    private IEnumerable<HarmonyMethod> GetOwnedMatchingPatch(PatchTarget target, PatchGroup group, Assembly patchAssembly, Category category)
        => GetOwnedPatch(target, group)
            .Where(x => IsPatchMatching(x, patchAssembly, category));

    private static bool IsPatchMatching(HarmonyMethod patchMethod, Category category)
        => patchMethod.category == category.Name;

    private static bool IsPatchMatching(HarmonyMethod patchMethod, Assembly patchAssembly)
        => patchMethod.method.Module.Assembly == patchAssembly;

    private static bool IsPatchMatching(HarmonyMethod patchMethod, Assembly patchAssembly, Category category)
        => IsPatchMatching(patchMethod, patchAssembly) && IsPatchMatching(patchMethod, category);
    
    // ---
    
    private readonly ConditionalWeakTable<Assembly, List<Type>> _allHarmonyPatchCache = new();
    private readonly ConditionalWeakTable<Assembly, Dictionary<Category, List<Type>>> _categoryPatchCache = new();

    private readonly IRuntimePatchRegistry _registry;
    private readonly ILogger _logger;

    public RuntimeRegistryBuilder(string id, IRuntimePatchRegistry registry, ILogger logger)
    {
        _registry = registry;
        _logger = logger;
        Id = id;
        
        _registry.AddInstance(Id);
    }

    private static Category GetCategory(Type type)
    {
        var harmonyAttributes = HarmonyMethodExtensions.GetFromType(type);
        if (harmonyAttributes.Count == 0) 
            return Category.Uncategorized;
        var containerAttributes = HarmonyMethod.Merge(harmonyAttributes);
        return new Category(containerAttributes.category);
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
        _logger.LogDebug("Found {Count} patch container types in assembly {Assembly}", result.Count, patchAssembly.FullName);
        return result;
    }
    
    private List<Type> GetMatchingTypes(Assembly patchAssembly, Category category)
    {
        if (!_categoryPatchCache.TryGetValue(patchAssembly, out var d))
        {
            d = [];
            _categoryPatchCache.Add(patchAssembly, d);
        }

        if (d.TryGetValue(category, out var result))
            return result;
        
        result = [];
        d.Add(category, result);
        
        var allTypes = GetAllTypes(patchAssembly);
        result.AddRange(allTypes.Where(typeDef => GetCategory(typeDef) == category).ToList());
        
        _logger.LogDebug("Found {Count} patch container types in assembly {Assembly} for category {Category}", result.Count, patchAssembly.FullName, category);
        return result;
    }

    private IEnumerable<Assembly> GetAllPatchAssemblies()
    {
        var preludeAsm = typeof(RuntimePrelude).Assembly;
        var preludeAsmName = preludeAsm.GetName().Name;
        
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (asm.GetReferencedAssemblies().All(asmRef => asmRef.Name != preludeAsmName))
                continue;

            _logger.LogDebug("Found patch assembly: {Assembly}", asm.FullName);
            yield return asm;
        }
    }
}