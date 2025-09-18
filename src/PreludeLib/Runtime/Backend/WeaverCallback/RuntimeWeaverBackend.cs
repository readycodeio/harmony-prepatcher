using System.Reflection;
using HarmonyLib;
using Microsoft.Extensions.Logging;
using PreludeLib.Runtime.Registry;

namespace PreludeLib.Runtime.Backend.WeaverCallback;

public class RuntimeWeaverBackend(ILogger logger) : IRuntimeBackend
{
    private readonly struct PatchEntry(MethodBase original, HarmonyMethod patchInfo, Delegate del, EventInfo ev)
    {
        public readonly MethodBase Original = original;
        public readonly HarmonyMethod PatchInfo = patchInfo;
        public readonly Delegate Del = del;
        public readonly EventInfo Event = ev;
    }
    
    private readonly Dictionary<MethodInfo, PatchEntry> _patchEntries = [];

    private void EnsurePatchEntry(MethodBase original, HarmonyMethod patchMethod, out PatchEntry patchEntry)
    {
        if (_patchEntries.TryGetValue(patchMethod.method, out patchEntry))
            return;

        var callbackType = original.Module.Assembly.GetType(
            $"{patchMethod.method.DeclaringType!.FullName}__{patchMethod.method.Name}__Callback", throwOnError: true
        )!;
        var ev = callbackType.GetEvent("Callback")!;

        patchEntry = default;
        
        if (ev.AddMethod == null)
            throw new InvalidOperationException($"Event {ev} has no add method");
        if (ev.RemoveMethod == null)
            throw new InvalidOperationException($"Event {ev} has no remove method");

        var delType = original.Module.Assembly.GetType(
            $"{patchMethod.method.DeclaringType!.FullName}__{patchMethod.method.Name}__DelegateType"
        )!;

        patchEntry = new PatchEntry(
            original,
            patchMethod,
            patchMethod.method.CreateDelegate(delType), 
            ev
        );
        _patchEntries.Add(patchMethod.method, patchEntry);
    }
    
    private bool IsSubset(IEnumerable<string> xs, IEnumerable<string> ys)
    {
        foreach (var x in xs)
        {
            if (!ys.Contains(x))
                return false;
        }
        
        return true;
    }

    private bool IsMatching(HarmonyMethod needle, HarmonyMethod haystack)
    {
        if (needle.method != haystack.method)
            return false;
        if (needle.methodType != haystack.methodType)
            return false;
        if (needle.declaringType != haystack.declaringType)
            return false;
        if (needle.argumentTypes.SequenceEqual(haystack.argumentTypes))
            return false;
        if (needle.priority != haystack.priority)
            return false;
        if (IsSubset(needle.before, haystack.before))
            return false;
        if (IsSubset(needle.after, haystack.after))
            return false;
        if (needle.category != haystack.category)
            return false;
        return true;
    }

    private void DoPatch(MethodBase original, HarmonyMethod patchMethod)
    {
        EnsurePatchEntry(original, patchMethod.method, out var patchEntry);
        patchEntry.Event.AddEventHandler(null, patchEntry.Del);
    }
    
    private void DoUnpatch(MethodBase original, HarmonyMethod patchMethod)
    {
        EnsurePatchEntry(original, patchMethod.method, out var patchEntry);
        patchEntry.Event.RemoveEventHandler(null, patchEntry.Del);
    }
    
    public void Commit(IRuntimePatchRegistry registry)
    {
        foreach (var id in registry.GetIds())
        {
            foreach (var group in registry.GetGroups())
            {
                foreach (var target in registry.GetTargets(group))
                {
                    var context = new AuxiliaryMethodCallContext(null!, group.ContainerType, null, null, logger);
                    var originals = RuntimeBackendUtils.GetTargetOriginals(target, context);

                    foreach (var original in originals)
                    {
                        foreach (var patchMethod in registry.GetRemovedPrefixMethods(target, id))
                        {
                            logger.LogInformation("Unpatching {Original} prefix {Prefix}", target, patchMethod.method);
                            DoUnpatch(original, patchMethod.method);
                        }
                    
                        foreach (var patchMethod in registry.GetRemovedPostfixMethods(target, id))
                        {
                            logger.LogInformation("Unpatching {Original} postfix {Postfix}", target, patchMethod.method);
                            DoUnpatch(original, patchMethod.method);
                        }
                    
                        foreach (var patchMethod in registry.GetRemovedFinalizerMethods(target, id))
                        {
                            logger.LogInformation("Unpatching {Original} finalizer {Finalizer}", target, patchMethod.method);
                            DoUnpatch(original, patchMethod.method);
                        }
                    
                        foreach (var patchMethod in registry.GetAddedPrefixMethods(target, id))
                        {
                            logger.LogInformation("Patching {Original} prefix {Prefix}", target, patchMethod.method);
                            DoPatch(original, patchMethod);
                        }
                    
                        foreach (var patchMethod in registry.GetAddedPostfixMethods(target, id))
                        {
                            logger.LogInformation("Patching {Original} postfix {Postfix}", target, patchMethod.method);
                            DoPatch(original, patchMethod);
                        }
                    
                        foreach (var patchMethod in registry.GetAddedFinalizerMethods(target, id))
                        {
                            logger.LogInformation("Patching {Original} finalizer {Finalizer}", target, patchMethod.method);
                            DoPatch(original, patchMethod);
                        }
                    }
                }
            }
        }
    }
}