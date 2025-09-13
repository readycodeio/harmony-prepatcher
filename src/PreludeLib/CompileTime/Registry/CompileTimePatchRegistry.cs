using HarmonyLib;
using Mono.Cecil;
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
    private readonly Dictionary<MethodDefinition, PatchEntry> _patchEntries = [];

    public IEnumerable<MethodDefinition> GetOriginalMethods()
        => _allOriginals;

    public IEnumerable<CompileTimePreludeMethod> GetPatchMethods(MethodDefinition original, HarmonyPatchType patchType)
    {
        EnsureOriginalMethodEntry(original, out var entry);
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

    public IEnumerable<CompileTimePreludeMethod> GetPrefixMethods(MethodDefinition original)
        => GetPatchMethods(original, HarmonyPatchType.Prefix);

    public IEnumerable<CompileTimePreludeMethod> GetPostfixMethods(MethodDefinition original)
        => GetPatchMethods(original, HarmonyPatchType.Postfix);

    public IEnumerable<CompileTimePreludeMethod> GetFinalizerMethods(MethodDefinition original)
        => GetPatchMethods(original, HarmonyPatchType.Finalizer);

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

    public void AddPatchMethod(MethodReference original, HarmonyPatchType patchType, CompileTimePreludeMethod patchMethod)
    {
        var resolved = original.Resolve();
        if (resolved is null)
            throw new ArgumentException($"Original method reference could not be resolved: {original.FullDescription()}");
        AddPatchMethod(resolved, patchType, patchMethod);
    }
    
    public void AddPatchMethod(MethodDefinition originalDef, HarmonyPatchType patchType, CompileTimePreludeMethod patchMethod)
    {
        var resolved = originalDef.Resolve();
        if (resolved is null)
            throw new ArgumentException("Original method reference could not be resolved");
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

    public void AddPatchMethod(MethodDefinition originalDef, CompileTimePreludePatch patchInfo)
    {
        AddPatchMethod(originalDef, patchInfo.PatchType, patchInfo.PatchMethod);
    }
}