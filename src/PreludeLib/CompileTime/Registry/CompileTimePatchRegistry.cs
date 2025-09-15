using HarmonyLib;
using Mono.Cecil;
using PreludeLib.Common;
using PreludeLib.CompileTime.Public;
using PreludeLib.CompileTime.Utils;

namespace PreludeLib.CompileTime.Registry;

public class CompileTimePatchRegistry : ICompileTimePatchRegistry
{
    private class PatchEntry(OriginalMethodEntry owner, HarmonyPatchType patchType, CompileTimePreludeMethod patchInfo)
    {
        public readonly OriginalMethodEntry Owner = owner;
        public readonly HarmonyPatchType PatchType = patchType;
        public readonly CompileTimePreludeMethod PatchInfo = patchInfo;
    }
    
    private class OriginalMethodEntry(MethodDefinition original)
    {
        public readonly MethodDefinition Original = original;
        public readonly List<PatchEntry> Prefixes = [];
        public readonly List<PatchEntry> Postfixes = [];
        public readonly List<PatchEntry> Finalizers = [];
    }
    
    private readonly List<MethodDefinition> _allOriginals = new();
    private readonly Dictionary<MethodDefinition, OriginalMethodEntry> _originalEntries = new();

    public IEnumerable<MethodDefinition> GetOriginalMethods()
        => _allOriginals;

    public IEnumerable<CompileTimePreludeMethod> GetPatchMethods(MethodDefinition originalDef, HarmonyPatchType patchType)
    {
        EnsureOriginalMethodEntry(originalDef, out var entry);
        return patchType switch
        {
            HarmonyPatchType.All => entry.Prefixes.Select(p => p.PatchInfo)
                .Concat(entry.Postfixes.Select(p => p.PatchInfo))
                .Concat(entry.Finalizers.Select(p => p.PatchInfo)),
            HarmonyPatchType.Prefix => entry.Prefixes.Select(p => p.PatchInfo),
            HarmonyPatchType.Postfix => entry.Postfixes.Select(p => p.PatchInfo),
            HarmonyPatchType.Finalizer => entry.Finalizers.Select(p => p.PatchInfo),
            HarmonyPatchType.Transpiler => [],
            _ => throw new ArgumentOutOfRangeException(nameof(patchType), patchType, null)
        };
    }

    public IEnumerable<CompileTimePreludeMethod> GetPrefixMethods(MethodDefinition originalDef)
        => GetPatchMethods(originalDef, HarmonyPatchType.Prefix);

    public IEnumerable<CompileTimePreludeMethod> GetPostfixMethods(MethodDefinition originalDef)
        => GetPatchMethods(originalDef, HarmonyPatchType.Postfix);

    public IEnumerable<CompileTimePreludeMethod> GetFinalizerMethods(MethodDefinition originalDef)
        => GetPatchMethods(originalDef, HarmonyPatchType.Finalizer);

    public IEnumerable<CompileTimePreludeMethod> GetCategoryPatchMethods(MethodDefinition originalDef, HarmonyPatchType patchType, Category category)
        => GetPatchMethods(originalDef, patchType).Where(x => x.Category == category.Name);

    public IEnumerable<CompileTimePreludeMethod> GetCategoryPrefixMethods(MethodDefinition originalDef, Category category)
        => GetPrefixMethods(originalDef).Where(x => x.Category == category.Name);

    public IEnumerable<CompileTimePreludeMethod> GetCategoryPostfixMethods(MethodDefinition originalDef, Category category)
        => GetPostfixMethods(originalDef).Where(x => x.Category == category.Name);

    public IEnumerable<CompileTimePreludeMethod> GetCategoryFinalizerMethods(MethodDefinition originalDef, Category category)
        => GetFinalizerMethods(originalDef).Where(x => x.Category == category.Name);
    
    public IEnumerable<CompileTimePreludeMethod> GetUncategorizedPatchMethods(MethodDefinition originalDef, HarmonyPatchType patchType)
        => GetCategoryPatchMethods(originalDef, patchType, Category.Uncategorized);

    public IEnumerable<CompileTimePreludeMethod> GetUncategorizedPrefixMethods(MethodDefinition originalDef)
        => GetCategoryPrefixMethods(originalDef, Category.Uncategorized);

    public IEnumerable<CompileTimePreludeMethod> GetUncategorizedPostfixMethods(MethodDefinition originalDef)
        => GetCategoryPostfixMethods(originalDef, Category.Uncategorized);

    public IEnumerable<CompileTimePreludeMethod> GetUncategorizedFinalizerMethods(MethodDefinition originalDef)
        => GetCategoryFinalizerMethods(originalDef, Category.Uncategorized);

    public void AddOriginalMethod(MethodReference originalRef)
    {
        var resolved = originalRef.Resolve();
        if (resolved is null)
            throw new ArgumentException($"Original method reference could not be resolved: {originalRef.FullDescription()}");
        AddOriginalMethod(resolved);
    }

    public void AddOriginalMethod(MethodDefinition originalDef)
    {
        if (_originalEntries.ContainsKey(originalDef))
            return;
        var entry = new OriginalMethodEntry(originalDef);
        _allOriginals.Add(originalDef);
        _originalEntries.Add(originalDef, entry);
    }
    
    private void EnsureOriginalMethodEntry(MethodDefinition originalDef, out OriginalMethodEntry entry)
    {
        if (!_originalEntries.TryGetValue(originalDef, out var e))
            throw new ArgumentException($"Original method not registered: {originalDef.FullDescription()}");
        entry = e;
    }

    public void AddPatchMethod(MethodReference originalRef, HarmonyPatchType patchType, CompileTimePreludeMethod patchMethod)
    {
        var resolved = originalRef.Resolve();
        if (resolved is null)
            throw new ArgumentException($"Original method reference could not be resolved: {originalRef.FullDescription()}", nameof(originalRef));
        AddPatchMethod(resolved, patchType, patchMethod);
    }

    public void AddPatchMethod(MethodDefinition originalDef, HarmonyPatchType patchType, CompileTimePreludeMethod patchMethod)
    {
        EnsureOriginalMethodEntry(originalDef, out var originalEntry);
        
        var patchEntry = new PatchEntry(originalEntry, patchType, patchMethod);
        if (patchType == HarmonyPatchType.All || patchType == HarmonyPatchType.Prefix)
            originalEntry.Prefixes.Add(patchEntry);
        if (patchType == HarmonyPatchType.All || patchType == HarmonyPatchType.Postfix)
            originalEntry.Postfixes.Add(patchEntry);
        if (patchType == HarmonyPatchType.All || patchType == HarmonyPatchType.Finalizer)
            originalEntry.Finalizers.Add(patchEntry);
        if (patchType == HarmonyPatchType.Transpiler)
            throw new NotSupportedException("Transpilers are not supported in CompileTimePatchRegistry");
    }

    public void AddPatchMethod(MethodReference originalRef, CompileTimePreludePatch patchInfo)
    {
        AddPatchMethod(originalRef, patchInfo.PatchType, patchInfo.PatchMethod);
    }
    
    public void AddPatchMethod(MethodDefinition originalDef, CompileTimePreludePatch patchInfo)
    {
        AddPatchMethod(originalDef, patchInfo.PatchType, patchInfo.PatchMethod);
    }
}