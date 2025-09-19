using System.Reflection;
using HarmonyLib;
using Microsoft.Extensions.Logging;
using PreludeLib.Runtime.Registry;

namespace PreludeLib.Runtime.Backend;

public abstract class RuntimePerIdPatchBackendBase(ILogger logger) : IRuntimeBackend
{
	protected readonly ILogger Logger = logger;
	
    protected abstract void DoPatch(
		MethodBase original,
		List<HarmonyMethod> prefixes,
		List<HarmonyMethod> postfixes,
		List<HarmonyMethod> finalizers,
		List<HarmonyMethod> addedPrefixes,
		List<HarmonyMethod> addedPostfixes,
		List<HarmonyMethod> addedFinalizers,
		List<HarmonyMethod> removedPrefixes,
		List<HarmonyMethod> removedPostfixes,
		List<HarmonyMethod> removedFinalizers,
		string id);
    
    protected abstract Harmony GetHarmonyInstance(string id);

    public virtual void Commit(IRuntimePatchRegistry registry)
    {
	    var openGroups = new List<(PatchGroup Group, string Id)>();
	    var openGroupsSet = new HashSet<(PatchGroup Group, string Id)>();
	    var originals = new List<MethodBase>();
	    var originalEntries = new Dictionary<MethodBase, List<(string Id, (List<PatchGroup> Groups, List<PatchTarget> Targets, List<PatchTarget> AddedTargets) Entry)>>();

	    void EnsureGroupOpen(PatchGroup group, string id)
	    {
		    if (!openGroupsSet.Add((group, id)))
			    return;
		    openGroups.Add((group, id));
	    }

	    void EnsureGroupClosed(PatchGroup group, string id)
	    {
		    if (!openGroupsSet.Remove((group, id)))
			    return;
	    }
	    
	    foreach (var id in registry.GetIds())
	    {
		    var harmony = GetHarmonyInstance(id);
		    
		    foreach (var group in registry.GetGroups(id))
		    {
			    foreach (var target in registry.GetTargets(group, id))
			    {
				    var isAdded = registry.HasAddedTarget(target, id);

				    if (target.IsFromTargetMethod)
				    {
					    EnsureGroupOpen(group, id);
				    }
				    
				    var context = new RuntimeAuxiliaryMethodContext(harmony, group.ContainerType, null, null, Logger);
				    var targetOriginals = RuntimeBackendUtils.GetTargetOriginals(target, context);

				    foreach (var original in targetOriginals)
				    {
					    (List<PatchGroup> Groups, List<PatchTarget> Targets, List<PatchTarget> AddedTargets) entry;
					    if (!originalEntries.TryGetValue(original, out var entries))
					    {
						    entries = new();
						    originalEntries.Add(original, entries);
						    originals.Add(original);
						}
					    
					    if (entries.Count == 0 || entries[entries.Count - 1].Id != id)
					    {
						    entry = (new List<PatchGroup>(), new List<PatchTarget>(), new List<PatchTarget>());
						    entries.Add((id, entry));
					    }
					    else
					    {
						    entry = entries[entries.Count - 1].Entry;
					    }

					    if (!entry.Groups.Contains(group))
						    entry.Groups.Add(group);
					    if (!entry.Targets.Contains(target))
						    entry.Targets.Add(target);
					    if (isAdded && !entry.AddedTargets.Contains(target))
						    entry.AddedTargets.Add(target);
				    }
			    }
		    }
		}
	    
	    var prefixes = new List<HarmonyMethod>();
	    var postfixes = new List<HarmonyMethod>();
	    var finalizers = new List<HarmonyMethod>();

	    var addedPrefixes = new List<HarmonyMethod>();
	    var addedPostfixes = new List<HarmonyMethod>();
	    var addedFinalizers = new List<HarmonyMethod>();

	    var removedPrefixes = new List<HarmonyMethod>();
	    var removedPostfixes = new List<HarmonyMethod>();
	    var removedFinalizers = new List<HarmonyMethod>();

	    foreach (var original in originals)
	    {
			var entries = originalEntries[original];

			foreach (var x in entries)
			{
				var id = x.Id;
				var entry = x.Entry;
				
				foreach (var group in entry.Groups)
				{
					EnsureGroupOpen(group, id);
				}

				prefixes.Clear();
				postfixes.Clear();
				finalizers.Clear();
		    
				addedPrefixes.Clear();
				addedPostfixes.Clear();
				addedFinalizers.Clear();
		    
				removedPrefixes.Clear();
				removedPostfixes.Clear();
				removedFinalizers.Clear();

				foreach (var target in entry.Targets)
				{
					prefixes.AddRange(registry.GetPrefixMethods(target, id));
					postfixes.AddRange(registry.GetPostfixMethods(target, id));
					finalizers.AddRange(registry.GetFinalizerMethods(target, id));

					addedPrefixes.AddRange(registry.GetAddedPrefixMethods(target, id));
					addedPostfixes.AddRange(registry.GetAddedPostfixMethods(target, id));
					addedFinalizers.AddRange(registry.GetAddedFinalizerMethods(target, id));

					removedPrefixes.AddRange(registry.GetRemovedPrefixMethods(target, id));
					removedPostfixes.AddRange(registry.GetRemovedPostfixMethods(target, id));
					removedFinalizers.AddRange(registry.GetRemovedFinalizerMethods(target, id));
				}
			
				DoPatch(
					original, 
					prefixes,
					postfixes,
					finalizers,
					addedPrefixes,
					addedPostfixes,
					addedFinalizers,
					removedPrefixes,
					removedPostfixes,
					removedFinalizers,
					id
				);
			}
	    }
	    
	    foreach (var d in openGroups)
	    {
		    EnsureGroupClosed(d.Group, d.Id);
	    }
    }
    
    private IEnumerable<HarmonyMethod> PrepareFixes(List<HarmonyMethod> fixes)
	    => fixes.Distinct().ToList();
}