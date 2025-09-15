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
        public readonly List<PatchEntry> AddedPrefixes = [];
        public readonly List<PatchEntry> AddedPostfixes = [];
        public readonly List<PatchEntry> AddedFinalizers = [];
    }
    
    private readonly List<MethodDefinition> _allOriginals = new();
    private readonly List<MethodDefinition> _addedOriginals = new();
    private readonly HashSet<MethodDefinition> _addedOriginalsSet = new();
    private readonly Dictionary<MethodDefinition, OriginalMethodEntry> _originalEntries = new();

    public IEnumerable<MethodDefinition> GetOriginalMethods()
        => _allOriginals;

    public IEnumerable<MethodDefinition> GetAddedOriginalMethods()
        => _addedOriginals;

    public bool HasOriginalMethod(MethodDefinition originalDef)
        => _originalEntries.ContainsKey(originalDef);

    public bool HasAddedOriginalMethod(MethodDefinition originalDef)
        => _addedOriginalsSet.Contains(originalDef);

    public IEnumerable<CompileTimePreludeMethod> GetPatchMethods(MethodDefinition originalDef, HarmonyPatchType patchType)
    {
        EnsureOriginalMethodEntry(originalDef, out var entry);
        var entries = patchType switch
        {
            HarmonyPatchType.All => entry.Prefixes
                .Concat(entry.Postfixes)
                .Concat(entry.Finalizers),
            HarmonyPatchType.Prefix => entry.Prefixes,
            HarmonyPatchType.Postfix => entry.Postfixes,
            HarmonyPatchType.Finalizer => entry.Finalizers,
            HarmonyPatchType.Transpiler => [],
            _ => throw new ArgumentOutOfRangeException(nameof(patchType), patchType, null)
        };
        
        return entries.Select(x => x.PatchInfo);
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

    public IEnumerable<CompileTimePreludeMethod> GetAddedPatchMethods(MethodDefinition originalDef, HarmonyPatchType patchType)
    {
        EnsureOriginalMethodEntry(originalDef, out var entry);
        var entries = patchType switch
        {
            HarmonyPatchType.All => entry.AddedPrefixes
                .Concat(entry.AddedPostfixes)
                .Concat(entry.AddedFinalizers),
            HarmonyPatchType.Prefix => entry.AddedPrefixes,
            HarmonyPatchType.Postfix => entry.AddedPostfixes,
            HarmonyPatchType.Finalizer => entry.AddedFinalizers,
            HarmonyPatchType.Transpiler => [],
            _ => throw new ArgumentOutOfRangeException(nameof(patchType), patchType, null)
        };
        
        return entries.Select(x => x.PatchInfo);
    }

    public IEnumerable<CompileTimePreludeMethod> GetAddedPrefixMethods(MethodDefinition originalDef)
        => GetAddedPatchMethods(originalDef, HarmonyPatchType.Prefix);

    public IEnumerable<CompileTimePreludeMethod> GetAddedPostfixMethods(MethodDefinition originalDef)
        => GetAddedPatchMethods(originalDef, HarmonyPatchType.Postfix);

    public IEnumerable<CompileTimePreludeMethod> GetAddedFinalizerMethods(MethodDefinition originalDef)
        => GetAddedPatchMethods(originalDef, HarmonyPatchType.Finalizer);

    public bool HasAddedPatchMethod(MethodDefinition originalDef, HarmonyPatchType patchType)
    {
        EnsureOriginalMethodEntry(originalDef, out var entry);
        return patchType switch
        {
            HarmonyPatchType.All => entry.AddedPrefixes.Any() ||
                                    entry.AddedPostfixes.Any() ||
                                    entry.AddedFinalizers.Any(),
            HarmonyPatchType.Prefix => entry.AddedPrefixes.Any(),
            HarmonyPatchType.Postfix => entry.AddedPostfixes.Any(),
            HarmonyPatchType.Finalizer => entry.AddedFinalizers.Any(),
            _ => false,
        };
    }

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
        _addedOriginals.Add(originalDef);
        _addedOriginalsSet.Add(originalDef);
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
            AddPatchMethod(originalEntry.Prefixes, originalEntry.AddedPrefixes, patchEntry);
        if (patchType == HarmonyPatchType.All || patchType == HarmonyPatchType.Postfix)
            AddPatchMethod(originalEntry.Postfixes, originalEntry.AddedPostfixes, patchEntry);
        if (patchType == HarmonyPatchType.All || patchType == HarmonyPatchType.Finalizer)
            AddPatchMethod(originalEntry.Finalizers, originalEntry.Finalizers, patchEntry);
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
    
    private void AddPatchMethod(List<PatchEntry> patches, List<PatchEntry> addedPatches, PatchEntry patchEntry)
    {
        var found = false;
        for (var i = 0; i < patches.Count; i++)
        {
            var patchItem = patches[i];
            if (patchItem.PatchInfo.Method == patchEntry.PatchInfo.Method)
            {
                found = true;
                break;
            }
        }
        
        if (found)
            throw new ArgumentException($"Patch method already registered: {patchEntry.PatchInfo.Method.FullDescription()}");

        patches.Add(patchEntry);
        addedPatches.Add(patchEntry);
    }

    public void ResetChanges()
    {
        _addedOriginals.Clear();
        _addedOriginalsSet.Clear();
        
        foreach (var entry in _originalEntries.Values)
        {
            entry.AddedPrefixes.Clear();
            entry.AddedPostfixes.Clear();
            entry.AddedFinalizers.Clear();
        }
    }
}