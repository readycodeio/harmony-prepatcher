extern alias OfficialCecil;
using HarmonyLib;
using Microsoft.Extensions.Logging;
using PreludeLib.Common;
using OfficialCecil::Mono.Cecil;
using PreludeLib.Common;
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
    
    public void ScanAndPatchCategory(AssemblyDefinition patchAssemblyDef, Category category)
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
        foreach (var typeDef in GetMatchingTypeDefs(patchAssemblyDef, Category.Uncategorized))
        {
            var category = GetCategory(typeDef);
            if (category.Name == null)
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
        var targets = GetBulkMethods(containerTypeDef);
        // NOTE: Skipping reverse patch feature
        // ReversePatch(ref lastOriginal);

        if (targets.Count > 0)
            BulkPatch(containerTypeDef, targets);
        else
            PatchWithAttributes(containerTypeDef);
    }
    
    private List<CompileTimePatchTarget> GetBulkMethods(TypeDefinition containerTypeDef)
    {
        logger.LogDebug("Getting bulk methods for patch container {ContainerType}", containerTypeDef.FullDescription());

        var harmonyAttributes = CompileTimePreludeMethodUtils.GetFromTypeDef(containerTypeDef);
        var containerAttributes = CompileTimePreludeMethod.Merge(harmonyAttributes);
        containerAttributes.MethodType ??= MethodType.Normal;

        var group = new CompileTimePatchGroup(containerTypeDef);
        var isPatchAll = containerTypeDef.CustomAttributes.Any(a => a.AttributeType.FullName == PatchTools.harmonyPatchAllFullName);
        if (isPatchAll)
        {
            var type = containerAttributes.DeclaringType;
            if (type is null)
                throw new ArgumentException($"Using {PatchTools.harmonyPatchAllFullName} requires an additional attribute for specifying the Class/Type");

            var list = new List<CompileTimePatchTarget>();
            list.AddRange(CompileTimeAccessTools.GetDeclaredConstructors(type).Select(x => CompileTimePatchTarget.FromOriginal(x, group)));
            list.AddRange(CompileTimeAccessTools.GetDeclaredMethods(type).Select(x => CompileTimePatchTarget.FromOriginal(x, group)));
            var props = CompileTimeAccessTools.GetDeclaredProperties(type);
            list.AddRange(props.Select(prop => prop.GetMethod).Where(method => method is not null).Select(x => CompileTimePatchTarget.FromOriginal(x, group)));
            list.AddRange(props.Select(prop => prop.SetMethod).Where(method => method is not null).Select(x => CompileTimePatchTarget.FromOriginal(x, group)));
            return list;
        }

        var harmonyTargetListMethod = CompileTimePatchTools.GetPatchMethod(containerTypeDef, typeof(HarmonyTargetMethods).FullName!);
        if (harmonyTargetListMethod != null)
        {
            var declaringType =  containerAttributes.DeclaringType?.Resolve();
            return [CompileTimePatchTarget.FromTargetMethod(harmonyTargetListMethod, declaringType, group)];
        }

        // var targetMethods = RunMethod<HarmonyTargetMethods, IEnumerable<MethodBase>>(null, null);
        /*
        if (targetMethods is object)
        {
            string error = null;
            result = [.. targetMethods];
            if (result is null)
                error = "null";
            else if (result.Any(m => m is null))
                error = "some element was null";
            if (error != null)
            {
                if (_auxiliaryMethods.TryGetValue(typeof(HarmonyTargetMethods), out var method))
                    throw new Exception($"Method {method.FullDescription()} returned an unexpected result: {error}");
                else
                    throw new Exception($"Some method returned an unexpected result: {error}");
            }
            return result;
        }
        */

        var harmonyTargetMethod = CompileTimePatchTools.GetPatchMethod(containerTypeDef, typeof(HarmonyTargetMethod).FullName!);
        if (harmonyTargetMethod != null)
        {
            var declaringType =  containerAttributes.DeclaringType?.Resolve();
            return [CompileTimePatchTarget.FromTargetMethod(harmonyTargetMethod, declaringType, group)];
        }

        // var targetMethod = RunMethod<HarmonyTargetMethod, MethodBase>(null, null, method => method is null ? "null" : null);
        /*
        if (targetMethod is not null)
            result.Add(targetMethod);
        */

        logger.LogDebug("No target method found for patch container {ContainerType}", containerTypeDef.FullName);
        return [];
    }

    private List<CompileTimeAttributePatch> GetPatchMethods(TypeDefinition containerTypeDef)
    {
        var harmonyAttributes = CompileTimePreludeMethodUtils.GetFromTypeDef(containerTypeDef);
        var containerAttributes = CompileTimePreludeMethod.Merge(harmonyAttributes);
        containerAttributes.MethodType ??= MethodType.Normal;
        
        List<CompileTimeAttributePatch> patchMethods = CompileTimePatchTools.GetPatchMethods(containerTypeDef);
        foreach (var patchMethod in patchMethods)
        {
            var method = patchMethod.Info.Method;
            patchMethod.Info = containerAttributes.Merge(patchMethod.Info);
            patchMethod.Info.Method = method;
        }

        return patchMethods;
    }
    
    private void BulkPatch(TypeDefinition containerTypeDef, List<CompileTimePatchTarget> targets)
    {
        var patchMethods = GetPatchMethods(containerTypeDef);

        var methodsList = CompileTimePreludeMethodUtils.GetFromTypeDef(containerTypeDef);
        var containerAttrs = CompileTimePreludeMethod.Merge(methodsList);
        containerAttrs.MethodType ??= MethodType.Normal;

        for (var i = 0; i < targets.Count; i++)
        {
            var lastTarget = targets[i];
            foreach (var patchMethod in patchMethods)
            {
                Patch(lastTarget, patchMethod);
            }
        }
    }
    
    private void PatchWithAttributes(TypeDefinition containerTypeDef)
    {
        var patchMethods = GetPatchMethods(containerTypeDef);

        if (patchMethods.Count == 0)
            logger.LogError("Patching with attributes for patch container {ContainerType} (NO PATCHES)", containerTypeDef.FullDescription());
        else
            logger.LogDebug("Patching with attributes for patch container {ContainerType} ({Count} patch methods)", containerTypeDef.FullDescription(), patchMethods.Count);

        var group = new CompileTimePatchGroup(containerTypeDef);
        foreach (var patchMethod in patchMethods)
        {
            var methodRef = patchMethod.Info.GetOriginalMethod();
            var lastTarget = CompileTimePatchTarget.FromOriginal(methodRef!.Resolve(), group);
            if (lastTarget.OriginalMethodDef is null)
                throw new ArgumentException($"Undefined target method for patch method {patchMethod.Info.Method.FullDescription()}");

            Patch(lastTarget, patchMethod);
        }
    }
    
    public void Patch(
        CompileTimePatchTarget target,
        CompileTimePreludeMethod? prefix = null,
        CompileTimePreludeMethod? postfix = null,
        CompileTimePreludeMethod? finalizer = null,
        CompileTimePreludeMethod? transpiler = null
    )
    {
        if (prefix != null)
            Patch(target, HarmonyPatchType.Prefix, prefix);
        if (postfix != null)
            Patch(target, HarmonyPatchType.Postfix, postfix);
        if (finalizer != null)
            Patch(target, HarmonyPatchType.Finalizer, finalizer);
        if (transpiler != null)
            throw new NotSupportedException("Transpilers are not supported.");
        // processor.AddInfix(infix);
    }
    
    public void Patch(CompileTimePatchTarget target, CompileTimeAttributePatch patch)
    {
        Patch(target, patch.PatchType, patch.Info);
    }

    public void Patch(CompileTimePatchTarget target, HarmonyPatchType patchType, CompileTimePreludeMethod patchMethod)
    {
        registry.AddGroup(target.Group);
        registry.AddTarget(target);
        registry.AddPatchMethod(target, patchType, patchMethod);
    }

    public void PatchPrefix(CompileTimePatchTarget target, CompileTimePreludeMethod prefix)
        => Patch(target, HarmonyPatchType.Prefix, prefix);

    public void PatchPostfix(CompileTimePatchTarget target, CompileTimePreludeMethod postfix)
        => Patch(target, HarmonyPatchType.Postfix, postfix);

    public void PatchFinalizer(CompileTimePatchTarget target, CompileTimePreludeMethod finalizer)
        => Patch(target, HarmonyPatchType.Finalizer, finalizer);
    
    // ---

    private readonly Dictionary<AssemblyDefinition, List<TypeDefinition>> _allHarmonyPatchCache = new();
    private readonly Dictionary<AssemblyDefinition, Dictionary<Category, List<TypeDefinition>>> _categoryPatchCache = new();

    private static Category GetCategory(TypeDefinition typeDef)
    {
        var harmonyAttributes = CompileTimePreludeMethodUtils.GetFromTypeDef(typeDef);
        if (harmonyAttributes.Count == 0) 
            return Category.Uncategorized;
        var containerAttributes = CompileTimePreludeMethod.Merge(harmonyAttributes);
        return new Category(containerAttributes.Category);
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
        logger.LogDebug("Found {Count} patch container types in assembly {Assembly}", result.Count, patchAssemblyDef.FullName);
        return result;
    }
    
    private List<TypeDefinition> GetMatchingTypeDefs(AssemblyDefinition patchAssemblyDef, Category category)
    {
        if (!_categoryPatchCache.TryGetValue(patchAssemblyDef, out var d))
        {
            d = [];
            _categoryPatchCache.Add(patchAssemblyDef, d);
        }

        if (d.TryGetValue(category, out var result))
            return result;

        result = [];
        d.Add(category, result);
        
        var allTypes = GetAllTypeDefs(patchAssemblyDef);
        result.AddRange(allTypes.Where(typeDef => GetCategory(typeDef) == category).ToList());
        
        logger.LogDebug("Found {Count} patch container types in assembly {Assembly} for category {Category}", result.Count, patchAssemblyDef.FullName, category);
        return result;
    }
}
