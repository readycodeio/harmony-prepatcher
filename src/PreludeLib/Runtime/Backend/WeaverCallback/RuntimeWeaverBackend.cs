using System.Diagnostics.CodeAnalysis;
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
            foreach (var original in registry.GetOriginalMethods())
            {
                foreach (var patchMethod in registry.GetRemovedPrefixMethods(original, id))
                {
                    logger.LogInformation("Unpatching {Original} prefix {Prefix}", original, patchMethod.method);
                    DoUnpatch(original, patchMethod.method);
                }
                
                foreach (var patchMethod in registry.GetRemovedPostfixMethods(original, id))
                {
                    logger.LogInformation("Unpatching {Original} postfix {Postfix}", original, patchMethod.method);
                    DoUnpatch(original, patchMethod.method);
                }
                
                foreach (var patchMethod in registry.GetRemovedFinalizerMethods(original, id))
                {
                    logger.LogInformation("Unpatching {Original} finalizer {Finalizer}", original, patchMethod.method);
                    DoUnpatch(original, patchMethod.method);
                }
                
                foreach (var patchMethod in registry.GetAddedPrefixMethods(original, id))
                {
                    logger.LogInformation("Patching {Original} prefix {Prefix}", original, patchMethod.method);
                    DoPatch(original, patchMethod);
                }
                
                foreach (var patchMethod in registry.GetAddedPostfixMethods(original, id))
                {
                    logger.LogInformation("Patching {Original} postfix {Postfix}", original, patchMethod.method);
                    DoPatch(original, patchMethod);
                }
                
                foreach (var patchMethod in registry.GetAddedFinalizerMethods(original, id))
                {
                    logger.LogInformation("Patching {Original} finalizer {Finalizer}", original, patchMethod.method);
                    DoPatch(original, patchMethod);
                }
            }
        }
    }
}