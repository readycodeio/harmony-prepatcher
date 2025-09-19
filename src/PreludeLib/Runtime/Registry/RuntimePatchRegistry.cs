using System.Reflection;
using HarmonyLib;
using PreludeLib.Common;

namespace PreludeLib.Runtime.Registry;

public class RuntimePatchRegistry : IRuntimePatchRegistry
{
    private struct IdEntry(string id)
    {
        public readonly string id = id;
        
        public readonly List<PatchGroup> Groups = [];
        public readonly List<PatchTarget> Targets = [];
        public readonly List<PatchTarget> AddedTargets = [];
        
        public readonly Dictionary<PatchGroup, GroupEntry> GroupEntries = [];
        public readonly Dictionary<PatchTarget, TargetEntry> TargetEntries = [];
        public readonly Dictionary<MethodInfo, PatchMethodEntry> PatchMethodEntries = [];
    }
    
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

    private readonly List<string> _ids = [];
    private readonly Dictionary<string, IdEntry> _idEntries = [];

    public IEnumerable<string> GetIds()
        => _ids;

    public bool HasId(string id)
        => _ids.Contains(id);

    public IEnumerable<PatchGroup> GetGroups(string id)
    {
        EnsureIdEntry(id, out var entry);
        return entry.Groups;
    }

    public bool HasGroup(PatchGroup group, string id)
    {
        EnsureIdEntry(id, out var entry);
        return entry.GroupEntries.ContainsKey(group);
    }

    public IEnumerable<PatchTarget> GetTargets(string id)
    {
        EnsureIdEntry(id, out var entry);
        return entry.Targets;
    }

    public IEnumerable<PatchTarget> GetTargets(PatchGroup group, string id)
    {
        EnsureIdEntry(id, out var idEntry);
        EnsureGroupEntry(idEntry, group, out var entry);
        return entry.Targets;
    }

    public IEnumerable<PatchTarget> GetAddedTargets(string id)
    {
        EnsureIdEntry(id, out var entry);
        return entry.AddedTargets;
    }

    public IEnumerable<PatchTarget> GetAddedTargets(PatchGroup group, string id)
    {
        EnsureIdEntry(id, out var idEntry);
        EnsureGroupEntry(idEntry, group, out var entry);
        return entry.AddedTargets;
    }
    
    public bool HasTarget(PatchTarget target, string id)
    {
        EnsureIdEntry(id, out var entry);
        return entry.TargetEntries.ContainsKey(target);
    }

    public bool HasAddedTarget(PatchTarget target, string id)
    {
        EnsureIdEntry(id, out var entry);
        return entry.AddedTargets.Contains(target);
    }

    public IEnumerable<HarmonyMethod> GetPatchMethods(PatchTarget target, string id, HarmonyPatchType patchType)
    {
        EnsureIdEntry(id, out var idEntry);
        EnsureTargetEntry(idEntry, target, out var entry);
        
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
        
        return entries.Select(p => p.PatchInfo);
    }

    public IEnumerable<HarmonyMethod> GetPrefixMethods(PatchTarget target, string id)
        => GetPatchMethods(target, id, HarmonyPatchType.Prefix);

    public IEnumerable<HarmonyMethod> GetPostfixMethods(PatchTarget target, string id)
        => GetPatchMethods(target, id, HarmonyPatchType.Postfix);

    public IEnumerable<HarmonyMethod> GetFinalizerMethods(PatchTarget target, string id)
        => GetPatchMethods(target, id, HarmonyPatchType.Finalizer);

    public IEnumerable<HarmonyMethod> GetCategoryPatchMethods(PatchTarget target, string id, HarmonyPatchType patchType, Category category)
        => GetPatchMethods(target, id, patchType).Where(x => x.category == category.Name);

    public IEnumerable<HarmonyMethod> GetCategoryPrefixMethods(PatchTarget target, string id, Category category)
        => GetPrefixMethods(target, id).Where(x => x.category == category.Name);

    public IEnumerable<HarmonyMethod> GetCategoryPostfixMethods(PatchTarget target, string id, Category category)
        => GetPostfixMethods(target, id).Where(x => x.category == category.Name);

    public IEnumerable<HarmonyMethod> GetCategoryFinalizerMethods(PatchTarget target, string id, Category category)
        => GetFinalizerMethods(target, id).Where(x => x.category == category.Name);

    public IEnumerable<HarmonyMethod> GetUncategorizedPatchMethods(PatchTarget target, string id, HarmonyPatchType patchType)
        => GetCategoryPatchMethods(target, id, patchType, Category.Uncategorized);

    public IEnumerable<HarmonyMethod> GetUncategorizedPrefixMethods(PatchTarget target, string id)
        => GetCategoryPrefixMethods(target, id, Category.Uncategorized);

    public IEnumerable<HarmonyMethod> GetUncategorizedPostfixMethods(PatchTarget target, string id)
        => GetCategoryPostfixMethods(target, id, Category.Uncategorized);

    public IEnumerable<HarmonyMethod> GetUncategorizedFinalizerMethods(PatchTarget target, string id)
        => GetCategoryFinalizerMethods(target, id, Category.Uncategorized);

    public IEnumerable<HarmonyMethod> GetAddedPatchMethods(PatchTarget target, string id, HarmonyPatchType patchType)
    {
        EnsureIdEntry(id, out var idEntry);
        EnsureTargetEntry(idEntry, target, out var entry);

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

        return entries.Select(p => p.PatchInfo);
    }

    public IEnumerable<HarmonyMethod> GetAddedPrefixMethods(PatchTarget target, string id)
        => GetAddedPatchMethods(target, id, HarmonyPatchType.Prefix);

    public IEnumerable<HarmonyMethod> GetAddedPostfixMethods(PatchTarget target, string id)
        => GetAddedPatchMethods(target, id, HarmonyPatchType.Postfix);

    public IEnumerable<HarmonyMethod> GetAddedFinalizerMethods(PatchTarget target, string id)
        => GetAddedPatchMethods(target, id, HarmonyPatchType.Finalizer);

    public bool HasAddedPatchMethod(PatchTarget target, string id, HarmonyPatchType patchType)
    {
        EnsureIdEntry(id, out var idEntry);
        EnsureTargetEntry(idEntry, target, out var entry);
        
        return patchType switch
        {
            HarmonyPatchType.All => entry.AddedPrefixes.Count > 0 ||
                                    entry.AddedPostfixes.Count > 0 ||
                                    entry.AddedFinalizers.Count > 0,
            HarmonyPatchType.Prefix => entry.AddedPrefixes.Count > 0,
            HarmonyPatchType.Postfix => entry.AddedPostfixes.Count > 0,
            HarmonyPatchType.Finalizer => entry.AddedFinalizers.Count > 0,
            _ => false,
        };
    }

    public IEnumerable<HarmonyMethod> GetRemovedPatchMethods(PatchTarget target, string id, HarmonyPatchType patchType)
    {
        EnsureIdEntry(id, out var idEntry);
        EnsureTargetEntry(idEntry, target, out var entry);
        
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
        
        return entries.Select(p => p.PatchInfo);
    }

    public IEnumerable<HarmonyMethod> GetRemovedPrefixMethods(PatchTarget target, string id)
        => GetRemovedPatchMethods(target, id, HarmonyPatchType.Prefix);

    public IEnumerable<HarmonyMethod> GetRemovedPostfixMethods(PatchTarget target, string id)
        => GetRemovedPatchMethods(target, id, HarmonyPatchType.Postfix);

    public IEnumerable<HarmonyMethod> GetRemovedFinalizerMethods(PatchTarget target, string id)
        => GetRemovedPatchMethods(target, id, HarmonyPatchType.Finalizer);

    public bool HasRemovedPatchMethod(PatchTarget target, string id, HarmonyPatchType patchType)
    {
        EnsureIdEntry(id, out var idEntry);
        EnsureTargetEntry(idEntry, target, out var entry);
        
        return patchType switch
        {
            HarmonyPatchType.All => entry.RemovedPrefixes.Count != 0 ||
                                    entry.RemovedPostfixes.Count != 0 ||
                                    entry.RemovedFinalizers.Count != 0,
            HarmonyPatchType.Prefix => entry.RemovedPrefixes.Count != 0,
            HarmonyPatchType.Postfix => entry.RemovedPostfixes.Count != 0,
            HarmonyPatchType.Finalizer => entry.RemovedFinalizers.Count != 0,
            _ => false,
        };
    }

    public MethodInfo? GetPrepareGroupCallback(PatchGroup group, string id)
    {
        EnsureIdEntry(id, out var idEntry);
        EnsureGroupEntry(idEntry, group, out var entry);
        return entry.PrepareCallback;
    }

    public MethodInfo? GetCleanupGroupCallback(PatchGroup group, string id)
    {
        EnsureIdEntry(id, out var idEntry);
        EnsureGroupEntry(idEntry, group, out var entry);
        return entry.CleanupCallback;
    }

    public MethodInfo? GetPreparePatchMethodCallback(HarmonyMethod patchMethod, string id)
    {
        EnsureIdEntry(id, out var idEntry);
        EnsurePatchMethodEntry(idEntry, patchMethod.method, out var entry);
        return entry.PrepareCallback;
    }

    public MethodInfo? GetCleanupPatchMethodCallback(HarmonyMethod patchMethod, string id)
    {
        EnsureIdEntry(id, out var idEntry);
        EnsurePatchMethodEntry(idEntry, patchMethod.method, out var entry);
        return entry.CleanupCallback;
    }

    public void AddInstance(string id)
    {
        if (_idEntries.ContainsKey(id))
            return;
        
        _ids.Add(id);
        var entry = new IdEntry(id);
        _idEntries.Add(id, entry);
    }

    public void AddGroup(PatchGroup group, string id)
    {
        EnsureIdEntry(id, out var idEntry);
        if (idEntry.GroupEntries.ContainsKey(group))
            return;
        idEntry.Groups.Add(group);
        var entry = new GroupEntry(group);
        idEntry.GroupEntries.Add(group, entry);
    }

    public void AddTarget(PatchTarget target, string id)
    {
        EnsureIdEntry(id, out var idEntry);
        if (idEntry.TargetEntries.ContainsKey(target))
            return;
        if (!idEntry.TargetEntries.ContainsKey(target))
        {
            idEntry.Targets.Add(target);
            idEntry.AddedTargets.Add(target);
            var entry = new TargetEntry(target);
            idEntry.TargetEntries.Add(target, entry);
        }
        EnsureGroupEntry(idEntry, target.Group, out var groupEntry);
        if (!groupEntry.Targets.Contains(target))
        {
            groupEntry.Targets.Add(target);
            groupEntry.AddedTargets.Add(target);
        }
    }

    public void AddPatchMethod(PatchTarget target, string id, HarmonyPatchType patchType, HarmonyMethod patchMethod)
    {
        EnsureIdEntry(id, out var idEntry);
        EnsureTargetEntry(idEntry, target, out var entry);
        
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

    public void RemovePatchMethod(PatchTarget target, string id, HarmonyPatchType patchType)
    {
        EnsureIdEntry(id, out var idEntry);
        EnsureTargetEntry(idEntry, target, out var entry);

        if (patchType == HarmonyPatchType.All || patchType == HarmonyPatchType.Prefix)
        {
            entry.RemovedPrefixes.AddRange(entry.Prefixes);
            entry.Prefixes.Clear();
            entry.AddedPrefixes.Clear();
        }

        if (patchType == HarmonyPatchType.All || patchType == HarmonyPatchType.Postfix)
        {
            entry.RemovedPostfixes.AddRange(entry.Postfixes);
            entry.Postfixes.Clear();
            entry.AddedPostfixes.Clear();
        }

        if (patchType == HarmonyPatchType.All || patchType == HarmonyPatchType.Finalizer)
        {
            entry.RemovedFinalizers.AddRange(entry.Finalizers);
            entry.Finalizers.Clear();
            entry.AddedFinalizers.Clear();
        }
    }

    public void RemovePatchMethod(PatchTarget target, string id, HarmonyMethod patchMethod)
    {
        EnsureIdEntry(id, out var idEntry);
        EnsureTargetEntry(idEntry, target, out var entry);

        var removed = RemovePatchMethod(entry.Prefixes, entry.AddedPrefixes, entry.RemovedPrefixes, patchMethod);
        removed |= RemovePatchMethod(entry.Postfixes, entry.AddedPostfixes, entry.RemovedPostfixes, patchMethod);
        removed |= RemovePatchMethod(entry.Finalizers, entry.AddedFinalizers, entry.RemovedFinalizers, patchMethod);
        
        if (!removed)
            throw new ArgumentException($"Patch method {patchMethod.Description()} not registered for target: {target.FullDescription()}");
    }

    public void RemovePatchMethod(PatchTarget target, string id, MethodInfo patchMethod)
        => RemovePatchMethod(target, id, new HarmonyMethod(patchMethod));

    public void SetPrepareGroupCallback(PatchGroup group, string id, MethodInfo? callback)
    {
        EnsureIdEntry(id, out var idEntry);
        EnsureGroupEntry(idEntry, group, out var entry);
        entry.PrepareCallback = callback;
        idEntry.GroupEntries[group] = entry;
    }

    public void SetCleanupGroupCallback(PatchGroup group, string id, MethodInfo? callback)
    {
        EnsureIdEntry(id, out var idEntry);
        EnsureGroupEntry(idEntry, group, out var entry);
        entry.CleanupCallback = callback;
        idEntry.GroupEntries[group] = entry;
    }

    public void SetPreparePatchMethodCallback(HarmonyMethod patchMethod, string id, MethodInfo? callback)
    {
        EnsureIdEntry(id, out var idEntry);
        EnsurePatchMethodEntry(idEntry, patchMethod.method, out var entry);
        idEntry.PatchMethodEntries[patchMethod.method] = entry;
    }

    public void SetCleanupPatchMethodCallback(HarmonyMethod patchMethod, string id, MethodInfo? callback)
    {
        EnsureIdEntry(id, out var idEntry);
        EnsurePatchMethodEntry(idEntry, patchMethod.method, out var entry);
        idEntry.PatchMethodEntries[patchMethod.method] = entry;
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
        HarmonyMethod item)
    {
        var found = false;
        PatchEntry entry = default;
        for (var i = 0; i < patches.Count; i++)
        {
            var patchItem = patches[i];
            if (patchItem.PatchInfo.method == item.method)
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
        foreach (var idEntry in _idEntries.Values)
        {
            idEntry.AddedTargets.Clear();
         
            foreach (var entry in idEntry.GroupEntries.Values)
            {
                entry.AddedTargets.Clear();
            }

            foreach (var entry in idEntry.TargetEntries.Values)
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

    private void EnsureIdEntry(string id, out IdEntry entry)
    {
        if (id == null)
            throw new ArgumentNullException(nameof(id));
        if (!_idEntries.TryGetValue(id, out var e))
            throw new ArgumentException($"ID not registered: {id}");
        entry = e;
    }
    
    private void EnsureGroupEntry(in IdEntry idEntry, PatchGroup group, out GroupEntry entry)
    {
        if (!idEntry.GroupEntries.TryGetValue(group, out var e))
            throw new ArgumentException($"Group not registered: {group.FullDescription()}");
        entry = e;
    }

    private void EnsureTargetEntry(in IdEntry idEntry, PatchTarget target, out TargetEntry entry)
    {
        if (!idEntry.TargetEntries.TryGetValue(target, out var e))
            throw new ArgumentException($"Patch target not registered: {target.FullDescription()}");
        entry = e;
    }

    private void EnsurePatchMethodEntry(in IdEntry idEntry, MethodInfo patch, out PatchMethodEntry entry)
    {
        if (!idEntry.PatchMethodEntries.TryGetValue(patch, out var e))
            throw new ArgumentException($"Container type method not registered: {patch.FullDescription()}");
        entry = e;
    }
}