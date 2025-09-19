using HarmonyLib;
using PreludeLib.Common;
using PreludeLib.CompileTime.Public;
using PreludeLib.CompileTime.Utils;

namespace PreludeLib.CompileTime.Registry;

public class CompileTimePatchRegistry : ICompileTimePatchRegistry
{
    private struct GroupEntry(CompileTimePatchGroup group)
    {
        public readonly CompileTimePatchGroup Group = group;
        
        public readonly List<CompileTimePatchTarget> Targets = [];
        public readonly List<CompileTimePatchTarget> AddedTargets = [];
    }
    
    private struct TargetEntry(CompileTimePatchTarget target)
    {
        public readonly CompileTimePatchTarget Target = target;
        public readonly List<PatchEntry> Prefixes = [];
        public readonly List<PatchEntry> Postfixes = [];
        public readonly List<PatchEntry> Finalizers = [];
        public readonly List<PatchEntry> AddedPrefixes = [];
        public readonly List<PatchEntry> AddedPostfixes = [];
        public readonly List<PatchEntry> AddedFinalizers = [];
    }

    private struct PatchEntry(TargetEntry owner, HarmonyPatchType patchType, CompileTimePreludeMethod patchInfo)
    {
        public readonly HarmonyPatchType PatchType = patchType;
        public readonly CompileTimePreludeMethod PatchInfo = patchInfo;
    }

    private readonly List<CompileTimePatchGroup> _allGroups = [];
    private readonly List<CompileTimePatchTarget> _allTargets = [];
    private readonly List<CompileTimePatchTarget> _addedTargets = [];
    private readonly HashSet<CompileTimePatchTarget> _addedTargetSet = [];
    private readonly Dictionary<CompileTimePatchGroup, GroupEntry> _groupEntries = [];
    private readonly Dictionary<CompileTimePatchTarget, TargetEntry> _targetEntries = new();

    public IEnumerable<CompileTimePatchGroup> GetGroups()
        => _allGroups;

    public bool HasGroup(CompileTimePatchGroup group)
        => _groupEntries.ContainsKey(group);

    public IEnumerable<CompileTimePatchTarget> GetTargets()
        => _allTargets;

    public IEnumerable<CompileTimePatchTarget> GetAddedTargets()
        => _addedTargets;

    public bool HasTarget(CompileTimePatchTarget target)
        => _targetEntries.ContainsKey(target);

    public bool HasAddedTarget(CompileTimePatchTarget target)
        => _addedTargetSet.Contains(target);

    public IEnumerable<CompileTimePatchTarget> GetTargets(CompileTimePatchGroup group)
    {
        EnsureGroupEntry(group, out var entry);
        return entry.Targets;
    }

    public IEnumerable<CompileTimePatchTarget> GetAddedTargets(CompileTimePatchGroup group)
    {
        EnsureGroupEntry(group, out var entry);
        return entry.AddedTargets;
    }

    public bool HasTarget(CompileTimePatchGroup group, CompileTimePatchTarget target)
    {
        EnsureGroupEntry(group, out var entry);
        return entry.Targets.Contains(target);
    }

    public bool HasAddedTarget(CompileTimePatchGroup group, CompileTimePatchTarget target)
    {
        EnsureGroupEntry(group, out var entry);
        return entry.AddedTargets.Contains(target);
    }

    public IEnumerable<CompileTimePreludeMethod> GetPatchMethods(CompileTimePatchTarget target, HarmonyPatchType patchType)
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
            HarmonyPatchType.Transpiler => [],
            _ => throw new ArgumentOutOfRangeException(nameof(patchType), patchType, null)
        };
        
        return entries.Select(x => x.PatchInfo);
    }

    public IEnumerable<CompileTimePreludeMethod> GetPrefixMethods(CompileTimePatchTarget target)
        => GetPatchMethods(target, HarmonyPatchType.Prefix);

    public IEnumerable<CompileTimePreludeMethod> GetPostfixMethods(CompileTimePatchTarget target)
        => GetPatchMethods(target, HarmonyPatchType.Postfix);

    public IEnumerable<CompileTimePreludeMethod> GetFinalizerMethods(CompileTimePatchTarget target)
        => GetPatchMethods(target, HarmonyPatchType.Finalizer);

    public IEnumerable<CompileTimePreludeMethod> GetCategoryPatchMethods(CompileTimePatchTarget target, HarmonyPatchType patchType, Category category)
        => GetPatchMethods(target, patchType).Where(x => x.Category == category.Name);

    public IEnumerable<CompileTimePreludeMethod> GetCategoryPrefixMethods(CompileTimePatchTarget target, Category category)
        => GetPrefixMethods(target).Where(x => x.Category == category.Name);

    public IEnumerable<CompileTimePreludeMethod> GetCategoryPostfixMethods(CompileTimePatchTarget target, Category category)
        => GetPostfixMethods(target).Where(x => x.Category == category.Name);

    public IEnumerable<CompileTimePreludeMethod> GetCategoryFinalizerMethods(CompileTimePatchTarget target, Category category)
        => GetFinalizerMethods(target).Where(x => x.Category == category.Name);
    
    public IEnumerable<CompileTimePreludeMethod> GetUncategorizedPatchMethods(CompileTimePatchTarget target, HarmonyPatchType patchType)
        => GetCategoryPatchMethods(target, patchType, Category.Uncategorized);

    public IEnumerable<CompileTimePreludeMethod> GetUncategorizedPrefixMethods(CompileTimePatchTarget target)
        => GetCategoryPrefixMethods(target, Category.Uncategorized);

    public IEnumerable<CompileTimePreludeMethod> GetUncategorizedPostfixMethods(CompileTimePatchTarget target)
        => GetCategoryPostfixMethods(target, Category.Uncategorized);

    public IEnumerable<CompileTimePreludeMethod> GetUncategorizedFinalizerMethods(CompileTimePatchTarget target)
        => GetCategoryFinalizerMethods(target, Category.Uncategorized);

    public IEnumerable<CompileTimePreludeMethod> GetAddedPatchMethods(CompileTimePatchTarget target, HarmonyPatchType patchType)
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
            HarmonyPatchType.Transpiler => [],
            _ => throw new ArgumentOutOfRangeException(nameof(patchType), patchType, null)
        };
        
        return entries.Select(x => x.PatchInfo);
    }

    public IEnumerable<CompileTimePreludeMethod> GetAddedPrefixMethods(CompileTimePatchTarget target)
        => GetAddedPatchMethods(target, HarmonyPatchType.Prefix);

    public IEnumerable<CompileTimePreludeMethod> GetAddedPostfixMethods(CompileTimePatchTarget target)
        => GetAddedPatchMethods(target, HarmonyPatchType.Postfix);

    public IEnumerable<CompileTimePreludeMethod> GetAddedFinalizerMethods(CompileTimePatchTarget target)
        => GetAddedPatchMethods(target, HarmonyPatchType.Finalizer);

    public bool HasAddedPatchMethod(CompileTimePatchTarget target, HarmonyPatchType patchType)
    {
        EnsureTargetEntry(target, out var entry);
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

    public void AddGroup(CompileTimePatchGroup group)
    {
        if (_groupEntries.ContainsKey(group))
            return;
        _allGroups.Add(group);
        var entry = new GroupEntry(group);
        _groupEntries.Add(group, entry);
    }

    public void AddTarget(CompileTimePatchTarget target)
    {
        var group = target.Group;
        EnsureGroupEntry(group, out var groupEntry);
        if (!_targetEntries.ContainsKey(target))
        {
            _allTargets.Add(target);
            _addedTargets.Add(target);
            _addedTargetSet.Add(target);
            var entry = new TargetEntry(target);
            _targetEntries.Add(target, entry);
        }
        if (!groupEntry.Targets.Contains(target))
        {
            groupEntry.Targets.Add(target);
            groupEntry.AddedTargets.Add(target);
        }
    }

    public void AddPatchMethod(CompileTimePatchTarget target, HarmonyPatchType patchType, CompileTimePreludeMethod patchMethod)
    {
        EnsureTargetEntry(target, out var originalEntry);
        
        var patchEntry = new PatchEntry(originalEntry, patchType, patchMethod);
        if (patchType == HarmonyPatchType.All || patchType == HarmonyPatchType.Prefix)
            AddPatchMethod(originalEntry.Prefixes, originalEntry.AddedPrefixes, patchEntry);
        if (patchType == HarmonyPatchType.All || patchType == HarmonyPatchType.Postfix)
            AddPatchMethod(originalEntry.Postfixes, originalEntry.AddedPostfixes, patchEntry);
        if (patchType == HarmonyPatchType.All || patchType == HarmonyPatchType.Finalizer)
            AddPatchMethod(originalEntry.Finalizers, originalEntry.AddedFinalizers, patchEntry);
        if (patchType == HarmonyPatchType.Transpiler)
            throw new NotSupportedException("Transpilers are not supported in CompileTimePatchRegistry");
    }
    
    public void AddPatchMethod(CompileTimePatchTarget target, CompileTimeAttributePatch patchInfo)
    {
        AddPatchMethod(target, patchInfo.PatchType, patchInfo.Info);
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
        _addedTargets.Clear();
        _addedTargetSet.Clear();
        
        foreach (var groupEntry in _groupEntries.Values)
        {
            groupEntry.AddedTargets.Clear();
        }
        
        foreach (var entry in _targetEntries.Values)
        {
            entry.AddedPrefixes.Clear();
            entry.AddedPostfixes.Clear();
            entry.AddedFinalizers.Clear();
        }
    }
    
    private void EnsureGroupEntry(CompileTimePatchGroup group, out GroupEntry entry)
    {
        if (!_groupEntries.TryGetValue(group, out var e))
            throw new ArgumentException($"Group not registered: {group.ContainerTypeDef.FullDescription()}");
        entry = e;
    }
    
    private void EnsureTargetEntry(CompileTimePatchTarget target, out TargetEntry entry)
    {
        if (!_targetEntries.TryGetValue(target, out var e))
            throw new ArgumentException($"Target not registered: {target.FullDescription()}");
        entry = e;
    }
}