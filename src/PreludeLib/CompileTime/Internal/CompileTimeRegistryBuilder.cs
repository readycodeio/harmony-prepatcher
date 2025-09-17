using HarmonyLib;
using Microsoft.Extensions.Logging;
using Mono.Cecil;
using PreludeLib.CompileTime.Public;
using PreludeLib.CompileTime.Registry;
using PreludeLib.CompileTime.Utils;

namespace PreludeLib.CompileTime.Internal;

internal class CompileTimeRegistryBuilder(ICompileTimePatchRegistry registry, ILogger logger) : ICompileTimeRegistryBuilder
{
    public void ScanAndPatchAll(AssemblyDefinition patchAssemblyDef)
    {
        foreach (var typeDef in GetAllTypeDefs(patchAssemblyDef))
        {
            ScanAndPatch(typeDef);
        }
    }
    
    public void ScanAndPatchCategory(AssemblyDefinition patchAssemblyDef, string category)
    {
        foreach (var typeDef in GetMatchingTypeDefs(patchAssemblyDef, category))
        {
            var typeCategory = GetCategory(typeDef);
            if (typeCategory == category)
            {
                ScanAndPatch(typeDef);
            }
        }
    }

    public void ScanAndPatchUncategorized(AssemblyDefinition patchAssemblyDef)
    {
        foreach (var typeDef in GetMatchingTypeDefs(patchAssemblyDef, null))
        {
            var category = GetCategory(typeDef);
            if (string.IsNullOrEmpty(category))
            {
                ScanAndPatch(typeDef);
            }
        }
    }

    public void ScanAndPatch(TypeReference containerTypeRef)
    {
        var resolved = containerTypeRef.Resolve();
        if (resolved is null)
            throw new ArgumentException($"Cannot resolve type reference: {containerTypeRef.FullName}");
        ScanAndPatch(resolved);
    }

    public void ScanAndPatch(TypeDefinition containerTypeDef)
    {
        var methodsList = CompileTimePreludeMethodUtils.GetFromTypeDef(containerTypeDef);
        
        var containerAttrs = CompileTimePreludeMethod.Merge(methodsList);
        containerAttrs.MethodType ??= MethodType.Normal;

        foreach (var auxMethodName in _auxiliaryMethodNames)
        {
            if (containerTypeDef.Methods.Any(m => m.Name == auxMethodName))
            {
                logger.LogWarning("Auxiliary methods are not supported in compile-time patching: {AuxMethod} in {ContainerType}", auxMethodName, containerTypeDef.FullName);
                // throw new ArgumentException($"Auxiliary methods are not supported in compile-time patching: {auxMethodName} in {containerTypeDef.FullName}");
            }
        }
        
        var patchMethods = CompileTimePreludeCecilUtils.GetPatchMethods(containerTypeDef);
        foreach (var patchMethod in patchMethods)
        {
            var method = patchMethod.PatchMethod.Method;
            patchMethod.PatchMethod = containerAttrs.Merge(patchMethod.PatchMethod);
            patchMethod.PatchMethod.Method = method;
            
            var original = patchMethod.PatchMethod.GetOriginalMethod();
            if (original == null)
                continue;
            
            Patch(original, patchMethod);
        }
    }
    
    public void Patch(
        MethodDefinition originalDef,
        CompileTimePreludeMethod? prefix = null,
        CompileTimePreludeMethod? postfix = null,
        CompileTimePreludeMethod? finalizer = null,
        CompileTimePreludeMethod? transpiler = null
    )
    {
        if (prefix != null)
            Patch(originalDef, HarmonyPatchType.Prefix, prefix);
        if (postfix != null)
            Patch(originalDef, HarmonyPatchType.Postfix, postfix);
        if (finalizer != null)
            Patch(originalDef, HarmonyPatchType.Finalizer, finalizer);
        if (transpiler != null)
            throw new NotSupportedException("Transpilers are not supported.");
        // processor.AddInfix(infix);
    }
    
    public void Patch(MethodReference originalDef, CompileTimePreludePatch patch)
    {
        Patch(originalDef, patch.PatchType, patch.PatchMethod);
    }

    public void Patch(MethodReference originalDef, HarmonyPatchType patchType, CompileTimePreludeMethod patchMethod)
    {
        registry.AddOriginalMethod(originalDef);
        registry.AddPatchMethod(originalDef, patchType, patchMethod);
    }

    public void PatchPrefix(MethodReference originalDef, CompileTimePreludeMethod prefix)
        => Patch(originalDef, HarmonyPatchType.Prefix, prefix);

    public void PatchPostfix(MethodReference originalDef, CompileTimePreludeMethod prefix)
        => Patch(originalDef, HarmonyPatchType.Postfix, prefix);

    public void PatchFinalizer(MethodReference originalDef, CompileTimePreludeMethod prefix)
        => Patch(originalDef, HarmonyPatchType.Finalizer, prefix);
    
    // ---
    
    private static readonly List<string> _auxiliaryMethodNames =
    [
        "Prepare",
        "Cleanup",
        "TargetMethod",
        "TargetMethods"
    ];

    private readonly Dictionary<AssemblyDefinition, List<TypeDefinition>> _allHarmonyPatchCache = new();
    private readonly Dictionary<AssemblyDefinition, Dictionary<string, List<TypeDefinition>>> _categoryPatchCache = new();
    private readonly Dictionary<AssemblyDefinition, List<TypeDefinition>> _uncategorizedPatchCache = new();

    private static string? GetCategory(TypeDefinition typeDef)
    {
        var harmonyAttributes = CompileTimePreludeMethodUtils.GetFromTypeDef(typeDef);
        if (harmonyAttributes.Count == 0) 
            return null;
        var containerAttributes = CompileTimePreludeMethod.Merge(harmonyAttributes);
        return containerAttributes.Category;
    }

    private List<TypeDefinition> GetAllTypeDefs(AssemblyDefinition patchAssemblyDef)
    {
        if (_allHarmonyPatchCache.TryGetValue(patchAssemblyDef, out var result))
            return result;
        
        result = [];
        foreach (var typeDef in CompileTimePreludeCecilUtils.GetTypesFromAssemblyDef(patchAssemblyDef))
        {
            if (!CompileTimePreludeCecilUtils.HasHarmonyAttributeDef(typeDef))
                continue;
            result.Add(typeDef);
        }

        _allHarmonyPatchCache.Add(patchAssemblyDef, result);
        return result;
    }
    
    private List<TypeDefinition> GetMatchingTypeDefs(AssemblyDefinition patchAssemblyDef, string? category)
    {
        List<TypeDefinition>? result;
        
        if (category == null)
        {
            if (!_uncategorizedPatchCache.TryGetValue(patchAssemblyDef, out result))
            {
                result = [];
                _uncategorizedPatchCache.Add(patchAssemblyDef, result);
            }
        }
        else
        {
            if (!_categoryPatchCache.TryGetValue(patchAssemblyDef, out var d))
            {
                d = [];
                _categoryPatchCache.Add(patchAssemblyDef, d);
            }
            if (!d.TryGetValue(category, out result))
            {
                result = [];
                d.Add(category, result);
            }
            
            d.Add(category, result);
        }
        
        var allTypes = GetAllTypeDefs(patchAssemblyDef);
        result.AddRange(allTypes.Where(typeDef => GetCategory(typeDef) == category).ToList());
        return result;
    }
}
