using HarmonyLib;
using Mono.Cecil;
using PreludeLib.CompileTime.Utils;

namespace PreludeLib.CompileTime.Public;

internal class CompileTimePreludeAttributeScanner(ICompileTimePreludeRegistryBuilder registryBuilder) : ICompileTimePreludeAttributeScanner
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
        var methodsList = CompileTimePreludeMethodExtensions.GetFromTypeDef(containerTypeDef);
        
        var containerAttrs = CompileTimePreludeMethod.Merge(methodsList);
        containerAttrs.MethodType ??= MethodType.Normal;

        foreach (var auxMethodName in _auxiliaryMethodNames)
        {
            if (containerTypeDef.Methods.Any(m => m.Name == auxMethodName))
                throw new ArgumentException($"Auxiliary methods are not supported in compile-time patching: {auxMethodName} in {containerTypeDef.FullName}");
        }

        var patches = CompileTimeCecilExtensions.GetPatches(containerTypeDef.Module, containerTypeDef);
        foreach (var patch in patches)
        {
            var methodRef = patch.PatchMethod?.Method;
            patch.PatchMethod = containerAttrs.Merge(patch.PatchMethod);
            patch.PatchMethod.Method = methodRef;

            var original = patch.PatchMethod.GetOriginalMethod();
            if (original == null)
                continue;
            
            registryBuilder.PatchAdd(original, patch);
        }
    }
    
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
        var harmonyAttributes = CompileTimePreludeMethodExtensions.GetFromTypeDef(typeDef);
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
        foreach (var typeDef in CompileTimeCecilExtensions.GetTypesFromAssemblyDef(patchAssemblyDef))
        {
            if (!CompileTimeCecilExtensions.HasHarmonyAttributeDef(typeDef))
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