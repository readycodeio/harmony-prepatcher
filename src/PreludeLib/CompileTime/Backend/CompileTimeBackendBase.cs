extern alias OfficialCecil;
using Microsoft.Extensions.Logging;
using OfficialCecil::Mono.Cecil;
using PreludeLib.CompileTime.Public;
using PreludeLib.CompileTime.Registry;

namespace PreludeLib.CompileTime.Backend;

public abstract class CompileTimeBackendBase(ILogger logger) : ICompileTimeBackend
{
	protected readonly ILogger Logger = logger;

	private readonly List<AssemblyDefinition> _patchedAssemblies = [];
	private readonly HashSet<AssemblyDefinition> _patchedAssembliesSet = [];

	protected abstract void DoPatch(
		MethodDefinition original,
		List<CompileTimePreludeMethod> prefixes,
		List<CompileTimePreludeMethod> postfixes,
		List<CompileTimePreludeMethod> finalizers,
		List<CompileTimePreludeMethod> addedPrefixes,
		List<CompileTimePreludeMethod> addedPostfixes,
		List<CompileTimePreludeMethod> addedFinalizers);

    public void Commit(ICompileTimePatchRegistry registry)
    {
	    var openGroups = new List<CompileTimePatchGroup>();
	    var openGroupsSet = new HashSet<CompileTimePatchGroup>();
	    var originals = new List<MethodDefinition>();
	    var originalEntries = new Dictionary<MethodDefinition, (List<CompileTimePatchGroup> Groups, List<CompileTimePatchTarget> Targets, List<CompileTimePatchTarget> AddedTargets)>();

	    void EnsureGroupOpen(CompileTimePatchGroup group)
	    {
		    if (!openGroupsSet.Add(group))
			    return;
		    openGroups.Add(group);
	    }

	    void EnsureGroupClosed(CompileTimePatchGroup group)
	    {
		    if (!openGroupsSet.Remove(group))
			    return;
	    }
	    
	    foreach (var group in registry.GetGroups())
	    {
		    foreach (var target in registry.GetTargets(group))
		    {
			    var isAdded = registry.HasAddedTarget(target);

			    if (target.IsFromTargetMethod)
			    {
				    EnsureGroupOpen(group);
			    }
			    
			    var context = new CompileTimeAuxiliaryMethodContext(group.ContainerTypeDef, null, null, Logger);
			    var targetOriginals = CompileTimeBackendUtils.GetTargetOriginals(target, context);

			    foreach (var original in targetOriginals)
			    {
				    if (!originalEntries.TryGetValue(original, out var entry))
				    {
					    entry = (new List<CompileTimePatchGroup>(), new List<CompileTimePatchTarget>(), new List<CompileTimePatchTarget>());
					    originalEntries.Add(original, entry);
					    originals.Add(original);
					}

				    if (!entry.Groups.Contains(group))
					    entry.Groups.Add(group);
				    if (isAdded && !entry.Targets.Contains(target))
					    entry.Targets.Add(target);
				    if (isAdded && !entry.AddedTargets.Contains(target))
					    entry.AddedTargets.Add(target);
			    }
		    }
	    }
	    
	    var prefixes = new List<CompileTimePreludeMethod>();
	    var postfixes = new List<CompileTimePreludeMethod>();
	    var finalizers = new List<CompileTimePreludeMethod>();
	    var addedPrefixes = new List<CompileTimePreludeMethod>();
	    var addedPostfixes = new List<CompileTimePreludeMethod>();
	    var addedFinalizers = new List<CompileTimePreludeMethod>();
	    
	    foreach (var original in originals)
	    {
			var entry = originalEntries[original];
			
			foreach (var group in entry.Groups)
			{
				EnsureGroupOpen(group);
			}
		
			prefixes.Clear();
			postfixes.Clear();
			finalizers.Clear();
			addedPrefixes.Clear();
			addedPostfixes.Clear();
			addedFinalizers.Clear();
			
			foreach (var target in entry.Targets)
			{
				prefixes.AddRange(registry.GetPrefixMethods(target));
				postfixes.AddRange(registry.GetPostfixMethods(target));
				finalizers.AddRange(registry.GetFinalizerMethods(target));
				addedPrefixes.AddRange(registry.GetAddedPrefixMethods(target));
				addedPostfixes.AddRange(registry.GetAddedPostfixMethods(target));
				addedFinalizers.AddRange(registry.GetAddedFinalizerMethods(target));
			}

			DoPatch(
				original, 
				prefixes, 
				postfixes,
				finalizers,
				addedPrefixes,
				addedPostfixes,
				addedFinalizers);

			if (_patchedAssembliesSet.Add(original.Module.Assembly))
			{
				_patchedAssemblies.Add(original.Module.Assembly);
			}
	    }
	    
	    foreach (var group in openGroups)
	    {
		    EnsureGroupClosed(group);
	    }
    }

    public IEnumerable<AssemblyDefinition> PatchedAssemblies
		=> _patchedAssemblies;
}