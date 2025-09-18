using System.Reflection;
using HarmonyLib;
using PreludeLib.Common;

namespace PreludeLib.Runtime.Registry;

public class RuntimePatchRegistry : IRuntimePatchRegistry
{
    private struct GroupEntry(PatchGroup group)
    {
        public readonly PatchGroup Group = group;

        public readonly List<PatchTarget> Targets = [];
        public readonly List<PatchTarget> AddedTargets = [];
        
        public MethodInfo? PrepareCallback;
        public MethodInfo? CleanupCallback;
    }
    
    private struct TargetEntry(PatchTarget target)
    {
        public readonly PatchTarget Target = target;
        
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
    
    private struct PatchEntry(TargetEntry owner, string id, HarmonyPatchType patchType, HarmonyMethod patchInfo)
    {
        public readonly string Id = id;
        public readonly HarmonyMethod PatchInfo = patchInfo;
        public readonly HarmonyPatchType PatchType = patchType;
        public readonly TargetEntry Owner = owner;
    }
    
    private struct PatchMethodEntry(HarmonyMethod patchMethod)
    {
        public readonly HarmonyMethod PatchMethod = patchMethod;
        
        public MethodInfo? PrepareCallback;
        public MethodInfo? CleanupCallback;
    }

    private readonly List<PatchGroup> _allGroups = [];
    private readonly List<string> _ids = [];
    private readonly List<string> _addedIds = [];
    private readonly Dictionary<PatchGroup, GroupEntry> _groupEntries = [];
    private readonly Dictionary<PatchTarget, TargetEntry> _targetEntries = [];
    private readonly Dictionary<MethodInfo, PatchMethodEntry> _patchMethodEntries = [];

    public IEnumerable<PatchGroup> GetGroups()
        => _allGroups;

    public bool HasGroup(PatchGroup group)
        => _groupEntries.ContainsKey(group);

    public IEnumerable<PatchTarget> GetTargets(PatchGroup group)
    {
        EnsureGroupEntry(group, out var entry);
        return entry.Targets;
    }

    public IEnumerable<PatchTarget> GetAddedTargets(PatchGroup group)
    {
        EnsureGroupEntry(group, out var entry);
        return entry.AddedTargets;
    }

    public bool HasTarget(PatchGroup group, PatchTarget target)
    {
        EnsureGroupEntry(group, out var entry);
        return entry.Targets.Contains(target);
    }

    public bool HasAddedTarget(PatchGroup group, PatchTarget target)
    {
        EnsureGroupEntry(group, out var entry);
        return entry.AddedTargets.Contains(target);
    }

    public IEnumerable<string> GetIds()
        => _ids;

    public IEnumerable<string> GetAddedIds()
        => _addedIds;

    public bool HasId(string id)
        => _ids.Contains(id);

    public bool HasAddedId(string id)
        => _addedIds.Contains(id);

    public IEnumerable<HarmonyMethod> GetPatchMethods(PatchTarget target, string? id, HarmonyPatchType patchType)
    {
        EnsureTargetEntry(target, out var entry);
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

    public IEnumerable<HarmonyMethod> GetPrefixMethods(PatchTarget target, string? id)
        => GetPatchMethods(target, id, HarmonyPatchType.Prefix);

    public IEnumerable<HarmonyMethod> GetPostfixMethods(PatchTarget target, string? id)
        => GetPatchMethods(target, id, HarmonyPatchType.Postfix);

    public IEnumerable<HarmonyMethod> GetFinalizerMethods(PatchTarget target, string? id)
        => GetPatchMethods(target, id, HarmonyPatchType.Finalizer);

    public IEnumerable<HarmonyMethod> GetCategoryPatchMethods(PatchTarget target, string? id, HarmonyPatchType patchType, Category category)
        => GetPatchMethods(target, id, patchType).Where(x => x.category == category.Name);

    public IEnumerable<HarmonyMethod> GetCategoryPrefixMethods(PatchTarget target, string? id, Category category)
        => GetPrefixMethods(target, id).Where(x => x.category == category.Name);

    public IEnumerable<HarmonyMethod> GetCategoryPostfixMethods(PatchTarget target, string? id, Category category)
        => GetPostfixMethods(target, id).Where(x => x.category == category.Name);

    public IEnumerable<HarmonyMethod> GetCategoryFinalizerMethods(PatchTarget target, string? id, Category category)
        => GetFinalizerMethods(target, id).Where(x => x.category == category.Name);

    public IEnumerable<HarmonyMethod> GetUncategorizedPatchMethods(PatchTarget target, string? id, HarmonyPatchType patchType)
        => GetCategoryPatchMethods(target, id, patchType, Category.Uncategorized);

    public IEnumerable<HarmonyMethod> GetUncategorizedPrefixMethods(PatchTarget target, string? id)
        => GetCategoryPrefixMethods(target, id, Category.Uncategorized);

    public IEnumerable<HarmonyMethod> GetUncategorizedPostfixMethods(PatchTarget target, string? id)
        => GetCategoryPostfixMethods(target, id, Category.Uncategorized);

    public IEnumerable<HarmonyMethod> GetUncategorizedFinalizerMethods(PatchTarget target, string? id)
        => GetCategoryFinalizerMethods(target, id, Category.Uncategorized);

    public IEnumerable<HarmonyMethod> GetAddedPatchMethods(PatchTarget target, string? id, HarmonyPatchType patchType)
    {
        EnsureTargetEntry(target, out var entry);
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

    public IEnumerable<HarmonyMethod> GetAddedPrefixMethods(PatchTarget target, string? id)
        => GetAddedPatchMethods(target, id, HarmonyPatchType.Prefix);

    public IEnumerable<HarmonyMethod> GetAddedPostfixMethods(PatchTarget target, string? id)
        => GetAddedPatchMethods(target, id, HarmonyPatchType.Postfix);

    public IEnumerable<HarmonyMethod> GetAddedFinalizerMethods(PatchTarget target, string? id)
        => GetAddedPatchMethods(target, id, HarmonyPatchType.Finalizer);

    public bool HasAddedPatchMethod(PatchTarget target, string? id, HarmonyPatchType patchType)
    {
        Func<PatchEntry, bool> predicate = id == null ? _ => true : x => x.Id == id;

        EnsureTargetEntry(target, out var entry);
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

    public IEnumerable<HarmonyMethod> GetRemovedPatchMethods(PatchTarget target, string? id, HarmonyPatchType patchType)
    {
        EnsureTargetEntry(target, out var entry);
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

    public IEnumerable<HarmonyMethod> GetRemovedPrefixMethods(PatchTarget target, string? id)
        => GetRemovedPatchMethods(target, id, HarmonyPatchType.Prefix);

    public IEnumerable<HarmonyMethod> GetRemovedPostfixMethods(PatchTarget target, string? id)
        => GetRemovedPatchMethods(target, id, HarmonyPatchType.Postfix);

    public IEnumerable<HarmonyMethod> GetRemovedFinalizerMethods(PatchTarget target, string? id)
        => GetRemovedPatchMethods(target, id, HarmonyPatchType.Finalizer);

    public bool HasRemovedPatchMethod(PatchTarget target, string? id, HarmonyPatchType patchType)
    {
        Func<PatchEntry, bool> predicate = id == null ? _ => true : x => x.Id == id;

        EnsureTargetEntry(target, out var entry);
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

    public MethodInfo? GetPrepareGroupCallback(PatchGroup group)
    {
        EnsureGroupEntry(group, out var entry);
        return entry.PrepareCallback;
    }

    public MethodInfo? GetCleanupGroupCallback(PatchGroup group)
    {
        EnsureGroupEntry(group, out var entry);
        return entry.CleanupCallback;
    }

    public MethodInfo? GetPreparePatchMethodCallback(HarmonyMethod patchMethod)
    {
        EnsurePatchMethodEntry(patchMethod.method, out var entry);
        return entry.PrepareCallback;
    }

    public MethodInfo? GetCleanupPatchMethodCallback(HarmonyMethod patchMethod)
    {
        EnsurePatchMethodEntry(patchMethod.method, out var entry);
        return entry.CleanupCallback;
    }

    public void AddGroup(PatchGroup group)
    {
        if (_groupEntries.ContainsKey(group))
            return;
        _allGroups.Add(group);
        var entry = new GroupEntry(group);
        _groupEntries.Add(group, entry);
    }

    public void AddTarget(PatchGroup group, PatchTarget target)
    {
        EnsureGroupEntry(group, out var groupEntry);
        if (groupEntry.Targets.Contains(target))
            return;
        if (_targetEntries.ContainsKey(target))
            throw new ArgumentException($"Patch target already registered in another group: {target.FullDescription()}");
        var entry = new TargetEntry(target);
        groupEntry.Targets.Add(target);
        groupEntry.AddedTargets.Add(target);
        _targetEntries.Add(target, entry);
    }

    public void AddPatchMethod(PatchTarget target, string id, HarmonyPatchType patchType, HarmonyMethod patchMethod)
    {
        EnsureTargetEntry(target, out var entry);
        EnsureId(id);
        
        var patchEntry = new PatchEntry(entry, id, patchType, patchMethod);
        if (patchType == HarmonyPatchType.All || patchType == HarmonyPatchType.Prefix)
            AddPatchMethod(entry.Prefixes, entry.AddedPrefixes, entry.RemovedPrefixes, patchEntry);
        if (patchType == HarmonyPatchType.All || patchType == HarmonyPatchType.Postfix)
            AddPatchMethod(entry.Postfixes, entry.AddedPostfixes, entry.RemovedPostfixes, patchEntry);
        if (patchType == HarmonyPatchType.All || patchType == HarmonyPatchType.Finalizer)
            AddPatchMethod(entry.Finalizers, entry.AddedFinalizers, entry.RemovedFinalizers, patchEntry);
        if (patchType == HarmonyPatchType.Transpiler)
            throw new NotSupportedException("Transpilers are not supported in CompileTimePatchRegistry");
    }

    public void RemovePatchMethod(PatchTarget target, string? id, HarmonyPatchType patchType)
    {
        Predicate<PatchEntry> predicate = id == null ? _ => true : x => x.Id == id;
        
        EnsureTargetEntry(target, out var entry);
        
        if (patchType == HarmonyPatchType.All || patchType == HarmonyPatchType.Prefix)
            entry.Prefixes.RemoveAll(predicate);
        if (patchType == HarmonyPatchType.All || patchType == HarmonyPatchType.Postfix)
            entry.Postfixes.RemoveAll(predicate);
        if (patchType == HarmonyPatchType.All || patchType == HarmonyPatchType.Finalizer)
            entry.Finalizers.RemoveAll(predicate);
    }

    public void RemovePatchMethod(PatchTarget target, string? id, HarmonyMethod patchMethod)
    {
        Func<PatchEntry, bool> predicate = id == null 
            ? (_ => true)
            : (x => x.Id == id);

        EnsureTargetEntry(target, out var entry);

        var removed = RemovePatchMethod(entry.Prefixes, entry.AddedPrefixes, entry.RemovedPrefixes, patchMethod, predicate);
        removed |= RemovePatchMethod(entry.Postfixes, entry.AddedPostfixes, entry.RemovedPostfixes, patchMethod, predicate);
        removed |= RemovePatchMethod(entry.Finalizers, entry.AddedFinalizers, entry.RemovedFinalizers, patchMethod, predicate);
        
        if (!removed)
            throw new ArgumentException($"Patch method {patchMethod.Description()} not registered for target: {target.FullDescription()}");
    }

    public void RemovePatchMethod(PatchTarget target, string? id, MethodInfo patchMethod)
        => RemovePatchMethod(target, id, new HarmonyMethod(patchMethod));

    public void SetPrepareGroupCallback(PatchGroup group, MethodInfo? callback)
    {
        EnsureGroupEntry(group, out var entry);
        entry.PrepareCallback = callback;
        _groupEntries[group] = entry;
    }

    public void SetCleanupGroupCallback(PatchGroup group, MethodInfo? callback)
    {
        EnsureGroupEntry(group, out var entry);
        entry.CleanupCallback = callback;
        _groupEntries[group] = entry;
    }

    public void SetPreparePatchMethodCallback(HarmonyMethod patchMethod, MethodInfo? callback)
    {
        EnsurePatchMethodEntry(patchMethod.method, out var entry);
        entry.PrepareCallback = callback;
        _patchMethodEntries[patchMethod.method] = entry;
    }

    public void SetCleanupPatchMethodCallback(HarmonyMethod patchMethod, MethodInfo? callback)
    {
        EnsurePatchMethodEntry(patchMethod.method, out var entry);
        entry.CleanupCallback = callback;
        _patchMethodEntries[patchMethod.method] = entry;
    }

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
            throw new ArgumentException($"Patch method {patchEntry.PatchInfo.Description()} already registered for target: {patchEntry.Owner.Target.FullDescription()}");

        if (!_patchMethodEntries.ContainsKey(patchEntry.PatchInfo.method))
        {
            _patchMethodEntries.Add(patchEntry.PatchInfo.method, new PatchMethodEntry(patchEntry.PatchInfo));
        }
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

        foreach (var entry in _groupEntries.Values)
        {
            entry.AddedTargets.Clear();
        }

        foreach (var entry in _targetEntries.Values)
        {
            entry.AddedPrefixes.Clear();
            entry.AddedPostfixes.Clear();
            entry.AddedFinalizers.Clear();
            entry.RemovedPrefixes.Clear();
            entry.RemovedPostfixes.Clear();
            entry.RemovedFinalizers.Clear();
        }
    }
    
    private void EnsureGroupEntry(PatchGroup group, out GroupEntry entry)
    {
        if (!_groupEntries.TryGetValue(group, out var e))
            throw new ArgumentException($"Group not registered: {group.FullDescription()}");
        entry = e;
    }

    private void EnsureTargetEntry(PatchTarget target, out TargetEntry entry)
    {
        if (!_targetEntries.TryGetValue(target, out var e))
            throw new ArgumentException($"Patch target not registered: {target.FullDescription()}");
        entry = e;
    }

    private void EnsurePatchMethodEntry(MethodInfo patch, out PatchMethodEntry entry)
    {
        if (!_patchMethodEntries.TryGetValue(patch, out var e))
            throw new ArgumentException($"Container type method not registered: {patch.FullDescription()}");
        entry = e;
    }

    private void EnsureId(string id)
    {
        if (_ids.Contains(id))
            return;
        _ids.Add(id);
        _addedIds.Add(id);
    }
}