using System.Reflection;
using HarmonyLib;
using Microsoft.Extensions.Logging;
using PreludeLib.Runtime.Registry;

namespace PreludeLib.Runtime.Backend;

public abstract class RuntimeGlobalPatchBackendBase(ILogger logger) : IRuntimeBackend
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
		List<HarmonyMethod> removedFinalizers);

    protected abstract Harmony GetHarmonyInstance(string id);

    public virtual void Commit(IRuntimePatchRegistry registry)
    {
	    var openGroups = new List<(PatchGroup Group, string Id)>();
	    var openGroupsSet = new HashSet<(PatchGroup Group, string Id)>();
	    var originals = new List<MethodBase>();
	    var originalEntries = new Dictionary<MethodBase, (List<(PatchGroup Group, string Id)> Groups, List<(PatchTarget Target, string Id)> Targets, List<(PatchTarget Target, string Id)> AddedTargets)>();

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
					    if (!originalEntries.TryGetValue(original, out var entry))
					    {
						    entry = (new List<(PatchGroup, string)>(), new List<(PatchTarget, string)>(), new List<(PatchTarget, string)>());
						    originalEntries.Add(original, entry);
						    originals.Add(original);
						}

					    if (!entry.Groups.Contains((group, id)))
						    entry.Groups.Add((group, id));
					    if (!entry.Targets.Contains((target, id)))
						    entry.Targets.Add((target, id));
					    if (isAdded && !entry.AddedTargets.Contains((target, id)))
						    entry.AddedTargets.Add((target, id));
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
			var entry = originalEntries[original];
			
			foreach (var d in entry.Groups)
			{
				EnsureGroupOpen(d.Group, d.Id);
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
			
			foreach (var d in entry.Targets)
			{
				prefixes.AddRange(registry.GetPrefixMethods(d.Target, d.Id));
				postfixes.AddRange(registry.GetPostfixMethods(d.Target, d.Id));
				finalizers.AddRange(registry.GetFinalizerMethods(d.Target, d.Id));
				addedPrefixes.AddRange(registry.GetAddedPrefixMethods(d.Target, d.Id));
				addedPostfixes.AddRange(registry.GetAddedPostfixMethods(d.Target, d.Id));
				addedFinalizers.AddRange(registry.GetAddedFinalizerMethods(d.Target, d.Id));
				removedPrefixes.AddRange(registry.GetRemovedPrefixMethods(d.Target, d.Id));
				removedPostfixes.AddRange(registry.GetRemovedPostfixMethods(d.Target, d.Id));
				removedFinalizers.AddRange(registry.GetRemovedFinalizerMethods(d.Target, d.Id));
			}

			DoPatch(original,
				prefixes,
				postfixes,
				finalizers,
				addedPrefixes,
				addedPostfixes,
				addedFinalizers,
				removedPrefixes,
				removedPostfixes,
				removedFinalizers);
	    }
	    
	    foreach (var d in openGroups)
	    {
		    EnsureGroupClosed(d.Group, d.Id);
	    }
    }
}