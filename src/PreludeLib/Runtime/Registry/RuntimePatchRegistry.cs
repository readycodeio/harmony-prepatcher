using System.Reflection;
using HarmonyLib;
using PreludeLib.Common;

namespace PreludeLib.Runtime.Registry;

public class RuntimePatchRegistry : IRuntimePatchRegistry
{
    private struct PatchEntry(OriginalMethodEntry owner, string id, HarmonyPatchType patchType, HarmonyMethod patchInfo)
    {
        public readonly string Id = id;
        public readonly HarmonyMethod PatchInfo = patchInfo;
        public readonly HarmonyPatchType PatchType = patchType;
        public readonly OriginalMethodEntry Owner = owner;
    }
    
    private struct OriginalMethodEntry(MethodBase original)
    {
        public readonly MethodBase Original = original;
        
        public readonly List<PatchEntry> Prefixes = [];
        public readonly List<PatchEntry> Postfixes = [];
        public readonly List<PatchEntry> Finalizers = [];
        
        public readonly List<PatchEntry> AddedPrefixes = [];
        public readonly List<PatchEntry> AddedPostfixes = [];
        public readonly List<PatchEntry> AddedFinalizers = [];
        
        public readonly List<PatchEntry> RemovedPrefixes = [];
        public readonly List<PatchEntry> RemovedPostfixes = [];
        public readonly List<PatchEntry> RemovedFinalizers = [];
    }

    private readonly List<MethodBase> _allOriginals = [];
    private readonly List<MethodBase> _addedOriginals = [];
    private readonly HashSet<MethodBase> _addedOriginalsSet = [];
    private readonly List<string> _ids = [];
    private readonly List<string> _addedIds = [];
    private readonly Dictionary<MethodBase, OriginalMethodEntry> _originalEntries = [];

    public IEnumerable<MethodBase> GetOriginalMethods()
        => _allOriginals;

    public IEnumerable<MethodBase> GetAddedOriginalMethods()
        => _addedOriginals;

    public bool HasOriginalMethod(MethodBase original)
        => _originalEntries.ContainsKey(original);

    public bool HasAddedOriginalMethod(MethodBase original)
        => _addedOriginalsSet.Contains(original);

    public IEnumerable<string> GetIds()
        => _ids;

    public IEnumerable<string> GetAddedIds()
        => _addedIds;

    public bool HasId(string id)
        => _ids.Contains(id);

    public bool HasAddedId(string id)
        => _addedIds.Contains(id);

    public IEnumerable<HarmonyMethod> GetPatchMethods(MethodBase original, string? id, HarmonyPatchType patchType)
    {
        EnsureOriginalMethodEntry(original, out var entry);
        var entries = patchType switch
        {
            HarmonyPatchType.All => entry.Prefixes
                .Concat(entry.Postfixes)
                .Concat(entry.Finalizers),
            HarmonyPatchType.Prefix => entry.Prefixes,
            HarmonyPatchType.Postfix => entry.Postfixes,
            HarmonyPatchType.Finalizer => entry.Finalizers,
            _ => [],
        };
        
        if (id != null)
            entries = entries.Where(x => x.Id == id);

        return entries.Select(p => p.PatchInfo);
    }

    public IEnumerable<HarmonyMethod> GetPrefixMethods(MethodBase original, string? id)
        => GetPatchMethods(original, id, HarmonyPatchType.Prefix);

    public IEnumerable<HarmonyMethod> GetPostfixMethods(MethodBase original, string? id)
        => GetPatchMethods(original, id, HarmonyPatchType.Postfix);

    public IEnumerable<HarmonyMethod> GetFinalizerMethods(MethodBase original, string? id)
        => GetPatchMethods(original, id, HarmonyPatchType.Finalizer);

    public IEnumerable<HarmonyMethod> GetCategoryPatchMethods(MethodBase original, string? id, HarmonyPatchType patchType, Category category)
        => GetPatchMethods(original, id, patchType).Where(x => x.category == category.Name);

    public IEnumerable<HarmonyMethod> GetCategoryPrefixMethods(MethodBase original, string? id, Category category)
        => GetPrefixMethods(original, id).Where(x => x.category == category.Name);

    public IEnumerable<HarmonyMethod> GetCategoryPostfixMethods(MethodBase original, string? id, Category category)
        => GetPostfixMethods(original, id).Where(x => x.category == category.Name);

    public IEnumerable<HarmonyMethod> GetCategoryFinalizerMethods(MethodBase original, string? id, Category category)
        => GetFinalizerMethods(original, id).Where(x => x.category == category.Name);

    public IEnumerable<HarmonyMethod> GetUncategorizedPatchMethods(MethodBase original, string? id, HarmonyPatchType patchType)
        => GetCategoryPatchMethods(original, id, patchType, Category.Uncategorized);

    public IEnumerable<HarmonyMethod> GetUncategorizedPrefixMethods(MethodBase original, string? id)
        => GetCategoryPrefixMethods(original, id, Category.Uncategorized);

    public IEnumerable<HarmonyMethod> GetUncategorizedPostfixMethods(MethodBase original, string? id)
        => GetCategoryPostfixMethods(original, id, Category.Uncategorized);

    public IEnumerable<HarmonyMethod> GetUncategorizedFinalizerMethods(MethodBase original, string? id)
        => GetCategoryFinalizerMethods(original, id, Category.Uncategorized);

    public IEnumerable<HarmonyMethod> GetAddedPatchMethods(MethodBase original, string? id, HarmonyPatchType patchType)
    {
        EnsureOriginalMethodEntry(original, out var entry);
        var entries = patchType switch
        {
            HarmonyPatchType.All => entry.AddedPrefixes
                .Concat(entry.AddedPostfixes)
                .Concat(entry.AddedFinalizers),
            HarmonyPatchType.Prefix => entry.AddedPrefixes,
            HarmonyPatchType.Postfix => entry.AddedPostfixes,
            HarmonyPatchType.Finalizer => entry.AddedFinalizers,
            _ => [],
        };
        
        if (id != null)
            entries = entries.Where(x => x.Id == id);

        return entries.Select(p => p.PatchInfo);
    }

    public IEnumerable<HarmonyMethod> GetAddedPrefixMethods(MethodBase original, string? id)
        => GetAddedPatchMethods(original, id, HarmonyPatchType.Prefix);

    public IEnumerable<HarmonyMethod> GetAddedPostfixMethods(MethodBase original, string? id)
        => GetAddedPatchMethods(original, id, HarmonyPatchType.Postfix);

    public IEnumerable<HarmonyMethod> GetAddedFinalizerMethods(MethodBase original, string? id)
        => GetAddedPatchMethods(original, id, HarmonyPatchType.Finalizer);

    public bool HasAddedPatchMethod(MethodBase original, string? id, HarmonyPatchType patchType)
    {
        Func<PatchEntry, bool> predicate = id == null ? _ => true : x => x.Id == id;

        EnsureOriginalMethodEntry(original, out var entry);
        return patchType switch
        {
            HarmonyPatchType.All => entry.AddedPrefixes.Any(predicate) ||
                                    entry.AddedPostfixes.Any(predicate) ||
                                    entry.AddedFinalizers.Any(predicate),
            HarmonyPatchType.Prefix => entry.AddedPrefixes.Any(predicate),
            HarmonyPatchType.Postfix => entry.AddedPostfixes.Any(predicate),
            HarmonyPatchType.Finalizer => entry.AddedFinalizers.Any(predicate),
            _ => false,
        };
    }

    public IEnumerable<HarmonyMethod> GetRemovedPatchMethods(MethodBase original, string? id, HarmonyPatchType patchType)
    {
        EnsureOriginalMethodEntry(original, out var entry);
        var entries = patchType switch
        {
            HarmonyPatchType.All => entry.RemovedPrefixes
                .Concat(entry.RemovedPostfixes)
                .Concat(entry.RemovedFinalizers),
            HarmonyPatchType.Prefix => entry.RemovedPrefixes,
            HarmonyPatchType.Postfix => entry.RemovedPostfixes,
            HarmonyPatchType.Finalizer => entry.RemovedFinalizers,
            _ => [],
        };
        
        if (id != null)
            entries = entries.Where(x => x.Id == id);

        return entries.Select(p => p.PatchInfo);
    }

    public IEnumerable<HarmonyMethod> GetRemovedPrefixMethods(MethodBase original, string? id)
        => GetRemovedPatchMethods(original, id, HarmonyPatchType.Prefix);

    public IEnumerable<HarmonyMethod> GetRemovedPostfixMethods(MethodBase original, string? id)
        => GetRemovedPatchMethods(original, id, HarmonyPatchType.Postfix);

    public IEnumerable<HarmonyMethod> GetRemovedFinalizerMethods(MethodBase original, string? id)
        => GetRemovedPatchMethods(original, id, HarmonyPatchType.Finalizer);

    public bool HasRemovedPatchMethod(MethodBase original, string? id, HarmonyPatchType patchType)
    {
        Func<PatchEntry, bool> predicate = id == null ? _ => true : x => x.Id == id;

        EnsureOriginalMethodEntry(original, out var entry);
        return patchType switch
        {
            HarmonyPatchType.All => entry.RemovedPrefixes.Any(predicate) ||
                                    entry.RemovedPostfixes.Any(predicate) ||
                                    entry.RemovedFinalizers.Any(predicate),
            HarmonyPatchType.Prefix => entry.RemovedPrefixes.Any(predicate),
            HarmonyPatchType.Postfix => entry.RemovedPostfixes.Any(predicate),
            HarmonyPatchType.Finalizer => entry.RemovedFinalizers.Any(predicate),
            _ => false,
        };
    }

    public void AddOriginalMethod(MethodBase original)
    {
        if (_originalEntries.ContainsKey(original))
            return;
        var entry = new OriginalMethodEntry(original);
        _allOriginals.Add(original);
        _addedOriginals.Add(original);
        _addedOriginalsSet.Add(original);
        _originalEntries.Add(original, entry);
    }

    private void EnsureOriginalMethodEntry(MethodBase original, out OriginalMethodEntry entry)
    {
        if (!_originalEntries.TryGetValue(original, out var e))
            throw new ArgumentException($"Original method not registered: {original.FullDescription()}");
        entry = e;
    }

    private void EnsureId(string id)
    {
        if (_ids.Contains(id))
            return;
        _ids.Add(id);
        _addedIds.Add(id);
    }

    public void AddPatchMethod(MethodBase original, string id, HarmonyPatchType patchType, HarmonyMethod patchMethod)
    {
        EnsureOriginalMethodEntry(original, out var originalEntry);
        EnsureId(id);
        
        var patchEntry = new PatchEntry(originalEntry, id, patchType, patchMethod);
        if (patchType == HarmonyPatchType.All || patchType == HarmonyPatchType.Prefix)
            AddPatchMethod(originalEntry.Prefixes, originalEntry.AddedPrefixes, originalEntry.RemovedPrefixes, patchEntry);
        if (patchType == HarmonyPatchType.All || patchType == HarmonyPatchType.Postfix)
            AddPatchMethod(originalEntry.Postfixes, originalEntry.AddedPostfixes, originalEntry.RemovedPostfixes, patchEntry);
        if (patchType == HarmonyPatchType.All || patchType == HarmonyPatchType.Finalizer)
            AddPatchMethod(originalEntry.Finalizers, originalEntry.AddedFinalizers, originalEntry.RemovedFinalizers, patchEntry);
        if (patchType == HarmonyPatchType.Transpiler)
            throw new NotSupportedException("Transpilers are not supported in CompileTimePatchRegistry");
    }

    public void RemovePatchMethod(MethodBase original, string? id, HarmonyPatchType patchType)
    {
        Predicate<PatchEntry> predicate = id == null ? _ => true : x => x.Id == id;
        
        EnsureOriginalMethodEntry(original, out var entry);
        
        if (patchType == HarmonyPatchType.All || patchType == HarmonyPatchType.Prefix)
            entry.Prefixes.RemoveAll(predicate);
        if (patchType == HarmonyPatchType.All || patchType == HarmonyPatchType.Postfix)
            entry.Postfixes.RemoveAll(predicate);
        if (patchType == HarmonyPatchType.All || patchType == HarmonyPatchType.Finalizer)
            entry.Finalizers.RemoveAll(predicate);
    }

    public void RemovePatchMethod(MethodBase original, string? id, HarmonyMethod patchMethod)
    {
        Func<PatchEntry, bool> predicate = id == null 
            ? (_ => true)
            : (x => x.Id == id);

        EnsureOriginalMethodEntry(original, out var entry);

        var removed = RemovePatchMethod(entry.Prefixes, entry.AddedPrefixes, entry.RemovedPrefixes, patchMethod, predicate);
        removed |= RemovePatchMethod(entry.Postfixes, entry.AddedPostfixes, entry.RemovedPostfixes, patchMethod, predicate);
        removed |= RemovePatchMethod(entry.Finalizers, entry.AddedFinalizers, entry.RemovedFinalizers, patchMethod, predicate);
        
        if (!removed)
            throw new ArgumentException($"Patch method {patchMethod.Description()} not registered for original: {original.FullDescription()}");
    }

    public void RemovePatchMethod(MethodBase original, string? id, MethodInfo patchMethod)
        => RemovePatchMethod(original, id, new HarmonyMethod(patchMethod));
    
    private void AddPatchMethod(
        List<PatchEntry> patches,
        List<PatchEntry> addedPatches,
        List<PatchEntry> removedPatches,
        PatchEntry patchEntry)
    {
        var found = false;
        for (var i = 0; i < patches.Count; i++)
        {
            var patchItem = patches[i];
            if (patchItem.PatchInfo.method == patchEntry.PatchInfo.method)
            {
                found = true;
                break;
            }
        }
        
        if (found)
            throw new ArgumentException($"Patch method {patchEntry.PatchInfo.Description()} already registered for original: {patchEntry.Owner.Original.FullDescription()}");

        patches.Add(patchEntry);
        
        var added = false;
        for (var i = 0; i < removedPatches.Count; i++)
        {
            var addedItem = removedPatches[i];
            if (addedItem.PatchInfo.method == patchEntry.PatchInfo.method)
            {
                added = true;
                removedPatches.RemoveAt(i);
                break;
            }
        }
        
        if (!added)
            addedPatches.Add(patchEntry);
    }

    private bool RemovePatchMethod(
        List<PatchEntry> patches,
        List<PatchEntry> addedPatches,
        List<PatchEntry> removedPatches,
        HarmonyMethod item, 
        Func<PatchEntry, bool> predicate)
    {
        var found = false;
        PatchEntry entry = default;
        for (var i = 0; i < patches.Count; i++)
        {
            var patchItem = patches[i];
            if (patchItem.PatchInfo.method == item.method && predicate(patchItem))
            {
                found = true;
                entry = patchItem;
                patches.RemoveAt(i);
                break;
            }
        }

        if (!found)
            return false;

        var removed = false;
        for (var i = 0; i < addedPatches.Count; i++)
        {
            var addedItem = addedPatches[i];
            if (addedItem.PatchInfo.method == item.method)
            {
                removed = true;
                addedPatches.RemoveAt(i);
                break;
            }
        }
        
        if (!removed)
            removedPatches.Add(entry);

        return true;
    }
    
    public void ResetChanges()
    {
        _addedIds.Clear();
        _addedOriginals.Clear();
        _addedOriginalsSet.Clear();

        foreach (var entry in _originalEntries.Values)
        {
            entry.AddedPrefixes.Clear();
            entry.AddedPostfixes.Clear();
            entry.AddedFinalizers.Clear();
            entry.RemovedPrefixes.Clear();
            entry.RemovedPostfixes.Clear();
            entry.RemovedFinalizers.Clear();
        }
    }
}