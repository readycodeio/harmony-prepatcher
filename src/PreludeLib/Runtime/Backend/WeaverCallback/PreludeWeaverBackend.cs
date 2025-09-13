using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using HarmonyLib;
using PreludeLib.Runtime.Utils;

namespace PreludeLib.Runtime.Backend.WeaverCallback;

public class PreludeWeaverBackend(string id) : IPreludeBackend
{
    private class PatchEntry(OriginalMethodEntry owner, HarmonyMethod patchInfo, Delegate del, EventInfo ev)
    {
        public bool IsPatched;
        public readonly HarmonyMethod PatchInfo = patchInfo;
        public readonly Delegate Del = del;
        public readonly EventInfo Event = ev;
        public readonly OriginalMethodEntry Owner = owner;
    }
    
    private class OriginalMethodEntry(MethodBase original)
    {
        public int PatchCount;
        public readonly MethodBase Original = original;
        public readonly List<PatchEntry> Prefixes = [];
        public readonly List<PatchEntry> Postfixes = [];
        public readonly List<PatchEntry> Finalizers = [];
    }
    
    public string Id { get; } = id;

    private readonly Dictionary<MethodBase, OriginalMethodEntry> _originalEntries = [];
    private readonly Dictionary<MethodInfo, PatchEntry> _patchEntries = [];
    private readonly List<MethodBase> _allPatched = [];

    public IPreludePatchProcessor CreateProcessor(MethodBase original)
        => new PreludeWeaverPatchProcessor(this, original);

    public IPreludeClassProcessor CreateClassProcessor(Type type)
        => new PreludeWeaverClassProcessor(this, type);

    private static readonly ConditionalWeakTable<Assembly, Dictionary<string, List<Type>>> _assemblyCachedCategories = new();

    // FIXME: This weird indirection was copied from Harmony implementation.
    public void PatchAll(Assembly patchAssembly)
        => AccessTools.GetTypesFromAssembly(patchAssembly).DoIf(type => type.HasHarmonyAttribute(), type => CreateClassProcessor(type).Patch());

    // FIXME: This weird indirection was copied from Harmony implementation.
    public void PatchCategory(Assembly patchAssembly, string category)
    {
        var categoryCache = _assemblyCachedCategories.GetValue(patchAssembly, BuildCategoryCache);
        if (categoryCache.TryGetValue(category, out var toPatch))
        {
            toPatch.Do(type => CreateClassProcessor(type).Patch());
        }
    }

    private static string? GetCategory(Type type)
    {
        var harmonyAttributes = HarmonyMethodExtensions.GetFromType(type);
        if (harmonyAttributes.Count == 0) 
            return null;
        var containerAttributes = HarmonyMethod.Merge(harmonyAttributes);
        return containerAttributes.category;
    }

    // FIXME: Memoize this
    private static Dictionary<string, List<Type>> BuildCategoryCache(Assembly assembly)
    {
        Dictionary<string, List<Type>> toBuild = [];
        foreach (var type in AccessTools.GetTypesFromAssembly(assembly))
        {
            var category = GetCategory(type);
            if (!string.IsNullOrEmpty(category))
            {
                if (!toBuild.TryGetValue(category, out var typeList))
                {
                    typeList ??= [];
                }
                typeList.Add(type);
                toBuild[category] = typeList;
            }
        }
        return toBuild;
    }

    public void PatchAllUncategorized(Assembly patchAssembly)
    {
        var patchClasses = AccessTools.GetTypesFromAssembly(patchAssembly).Where(type => type.HasHarmonyAttribute()).Select(CreateClassProcessor).ToArray();
        patchClasses.DoIf(patchClass => string.IsNullOrEmpty(patchClass.Category), patchClass => patchClass.Patch());
    }

    public void Patch(
        MethodBase original, 
        HarmonyMethod? prefix = null,
        HarmonyMethod? postfix = null,
        HarmonyMethod? finalizer = null,
        HarmonyMethod? transpiler = null)
    {
        var processor = CreateProcessor(original);
        processor.AddPrefix(prefix);
        processor.AddPostfix(postfix);
        processor.AddTranspiler(transpiler);
        processor.AddFinalizer(finalizer);
        // processor.AddInfix(infix);
        processor.Patch();
    }

    public void UnpatchAll()
    {
        foreach (var original in _allPatched) // all originals
        {
            foreach (var patchEntry in GetOwnedPatch(original))
            {
                DoUnpatch(patchEntry);
            }
        }
    }

    public void UnpatchAll(Assembly patchAssembly)
    {
        foreach (var original in _allPatched) // all originals
        {
            foreach (var patchEntry in GetOwnedMatchingPatch(original, patchAssembly))
            {
                DoUnpatch(patchEntry);
            }
        }
    }

    public void UnpatchCategory(Assembly patchAssembly, string category)
    {
        foreach (var original in _allPatched) // all originals
        {
            foreach (var patchEntry in GetOwnedMatchingPatch(original, patchAssembly, category))
            {
                DoUnpatch(patchEntry);
            }
        }
    }

    public void UnpatchUncategorized(Assembly patchAssembly)
    {
        foreach (var original in _allPatched) // all originals
        {
            foreach (var patchEntry in GetOwnedMatchingPatch(original, category: null))
            {
                DoUnpatch(patchEntry);
            }
        }
    }

    public void UnpatchCategory(string category)
    {
        foreach (var original in _allPatched) // all originals
        {
            foreach (var patchEntry in GetOwnedMatchingPatch(original, category))
            {
                DoUnpatch(patchEntry);
            }
        }
    }

    public void UnpatchUncategorized()
    {
        foreach (var original in _allPatched) // all originals
        {
            foreach (var patchEntry in GetOwnedMatchingPatch(original, category: null))
            {
                DoUnpatch(patchEntry);
            }
        }
    }

    public void Unpatch(MethodBase original, HarmonyPatchType patchType)
    {
        DoUnpatch(original, patchType);
    }

    public void Unpatch(MethodBase original, MethodInfo patch)
    {
        DoUnpatch(original, patch);
    }
    
    // ---

    private void EnsureOriginalEntry(Type type)
    {
        void EnsureOriginalMethodEntry(MethodBase original, out OriginalMethodEntry entry)
        {
            if (!_originalEntries.TryGetValue(original, out entry))
            {
                entry = new OriginalMethodEntry(original);
                _originalEntries[original] = entry;
            }
        }
        
        var events = type.GetEvents();

        foreach (var ev in events)
        {
            if (ev.AddMethod == null)
                continue;
            if (ev.RemoveMethod == null)
                continue;
            if (ev.AddMethod?.IsStatic != true)
                continue;
            
            if (ev.IsDefined(typeof(WeaverCallbackAttribute), false))
            {
                var alc = AssemblyLoadContext.GetLoadContext(type.Module.Assembly);
                var alcName = alc?.Name;
                
                var attr = ev.GetCustomAttribute<WeaverCallbackAttribute>();
                if (attr == null)
                    continue;
                var original = attr.GetOriginalMethod(alcName);
                if (original == null)
                    continue;
                var patchMethod = attr.GetPatchMethod(alcName);
                if (patchMethod == null)
                    continue;
                
                EnsureOriginalMethodEntry(original, out var originalEntry);

                var patchEntry = new PatchEntry(
                    originalEntry,
                    new HarmonyMethod(patchMethod), 
                    MethodUtils.CreateDelegate(patchMethod), 
                    ev
                );
                _patchEntries.Add(patchMethod, patchEntry);
                
                switch (attr.PatchType)
                {
                    case HarmonyPatchType.Prefix:
                        originalEntry.Prefixes.Add(patchEntry);
                        break;
                    case HarmonyPatchType.Postfix:
                        originalEntry.Postfixes.Add(patchEntry);
                        break;
                    case HarmonyPatchType.Finalizer:
                        originalEntry.Finalizers.Add(patchEntry);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }
    }
    
    private void EnsureOriginalEntry(MethodBase original)
    {
        if (_originalEntries.ContainsKey(original))
            return;

        if (original.DeclaringType == null)
            throw new ArgumentException($"Original method has no declaring type: {original}", nameof(original));
        
        EnsureOriginalEntry(original.DeclaringType);
    }
    
    private bool IsOwnedPatch(MethodBase original, MethodInfo patch, [NotNullWhen(true)] out PatchEntry? patchEntry)
    {
        if (!_originalEntries.TryGetValue(original, out var entry))
        {
            patchEntry = null;
            return false;
        }

        if (entry.Prefixes.All(x => x.PatchInfo.method != patch) &&
            entry.Postfixes.All(x => x.PatchInfo.method != patch) &&
            entry.Finalizers.All(x => x.PatchInfo.method != patch))
        {
            patchEntry = null;
            return false;
        }

        patchEntry = _patchEntries[patch];
        return true;
    }
    
    private void EnsurePatchOwned(MethodBase original, MethodInfo patch, out PatchEntry patchEntry)
    {
        if (!IsOwnedPatch(original, patch, out var p))
            throw new ArgumentException($"The specified patch `{patch}` was not found (missing from preprocessing?).", nameof(patch));
        
        patchEntry = p;
    }
    
    private void EnsurePatchOwned(MethodBase original, HarmonyMethod patchInfo, out PatchEntry patchEntry)
    {
        if (!IsOwnedPatch(original, patchInfo.method, out var p))
            throw new ArgumentException($"The specified patch `{patchInfo}` was not found (missing from preprocessing?).", nameof(patchInfo));

        EnsurePatchMatching(p, patchInfo);
        patchEntry = p;
    }

    private void EnsurePatchMatching(PatchEntry patchEntry, HarmonyMethod patchInfo)
    {
        if (!IsPatchMatching(patchEntry, patchInfo))
            throw new ArgumentException($"The specified patch `{patchInfo}` did not match the " +
                                        $"preprocessed patch {patchEntry.PatchInfo} (preprocessing mismatch?).", nameof(patchInfo));
    }
    
    private IEnumerable<PatchEntry> GetOwnedPatch(MethodBase? original)
    {
        if (original == null)
            return [];

        if (!_originalEntries.TryGetValue(original, out var entry))
            return [];

        return entry.Prefixes
            .Concat(entry.Postfixes)
            .Concat(entry.Finalizers);
    }
    
    private IEnumerable<PatchEntry> GetOwnedMatchingPatch(MethodBase? original, string? category)
        => GetOwnedPatch(original)
            .Where(x => IsPatchMatching(x, category));

    private IEnumerable<PatchEntry> GetOwnedMatchingPatch(MethodBase? original, Assembly patchAssembly)
        => GetOwnedPatch(original)
            .Where(x => IsPatchMatching(x, patchAssembly));

    private IEnumerable<PatchEntry> GetOwnedMatchingPatch(MethodBase? original, Assembly patchAssembly, string? category)
        => GetOwnedPatch(original)
            .Where(x => IsPatchMatching(x, patchAssembly, category));

    private static bool IsPatchMatching(PatchEntry patch, string? category)
        => patch.PatchInfo.category == category;

    private static bool IsPatchMatching(PatchEntry patch, Assembly patchAssembly)
        => patch.PatchInfo.method.Module.Assembly == patchAssembly;

    private static bool IsPatchMatching(PatchEntry patch, Assembly patchAssembly, string? category)
        => IsPatchMatching(patch, patchAssembly) && IsPatchMatching(patch, category);

    private static bool IsPatchMatching(PatchEntry patch, HarmonyMethod patchInfo)
    {
        if (patch.PatchInfo.method != patchInfo.method)
            return false;
        if (patch.PatchInfo.category != patchInfo.category)
            return false;
        if (patch.PatchInfo.declaringType != patchInfo.declaringType)
            return false;
        if (patch.PatchInfo.methodName != patchInfo.methodName)
            return false;
        if (patch.PatchInfo.argumentTypes?.Length != patchInfo.argumentTypes?.Length)
            return false;
        var count = patch.PatchInfo.argumentTypes?.Length ?? 0;
        for (var i = 0; i < count; i++)
        {
            if (patch.PatchInfo.argumentTypes![i] != patchInfo.argumentTypes![i])
                return false;
        }
        if (patch.PatchInfo.priority != patchInfo.priority)
            return false;
        foreach (var b in patchInfo.before)
        {
            if (!patch.PatchInfo.before.Contains(b))
                return false;
        }
        foreach (var a in patchInfo.after)
        {
            if (!patch.PatchInfo.after.Contains(a))
                return false;
        }

        return true;
    }
    
    internal void DoPatch(MethodBase original, MethodInfo patch)
    {
        EnsurePatchOwned(original, patch, out var patchEntry);
        DoPatch(patchEntry);
    }

    internal void DoPatch(MethodBase original, HarmonyMethod patchInfo)
    {
        EnsurePatchOwned(original, patchInfo, out var patchEntry);
        DoPatch(patchEntry);
    }

    private void DoPatch(PatchEntry patchInfo)
    {
        if (patchInfo.IsPatched)
            return;
        if (patchInfo.Owner.PatchCount == 0)
        {
            _allPatched.Add(patchInfo.Owner.Original);
        }
        patchInfo.IsPatched = true;
        patchInfo.Owner.PatchCount++;
        patchInfo.Event.AddEventHandler(null, patchInfo.Del);
    }
    
    internal void DoUnpatch(MethodBase original, MethodInfo patch)
    {
        EnsurePatchOwned(original, patch, out var patchEntry);
        DoUnpatch(patchEntry);
    }
    
    private void DoUnpatch(PatchEntry patchInfo)
    {
        if (!patchInfo.IsPatched)
            return;
        patchInfo.IsPatched = false;
        patchInfo.Owner.PatchCount--;
        patchInfo.Event.RemoveEventHandler(null, patchInfo.Del);
        if (patchInfo.Owner.PatchCount == 0)
        {
            _allPatched.Remove(patchInfo.Owner.Original);
        }
    }

    public void DoUnpatch(MethodBase original, HarmonyPatchType patchType)
    {
        if (!_originalEntries.TryGetValue(original, out var entry))
            return;
        if (patchType == HarmonyPatchType.All || patchType == HarmonyPatchType.Prefix)
        {
            foreach (var patchEntry in entry.Prefixes)
            {
                DoUnpatch(patchEntry);
            }
        }

        if (patchType == HarmonyPatchType.All || patchType == HarmonyPatchType.Postfix)
        {
            foreach (var patchEntry in entry.Postfixes)
            {
                DoUnpatch(patchEntry);
            }
        }
        
        if (patchType == HarmonyPatchType.All || patchType == HarmonyPatchType.Finalizer)
        {
            foreach (var patchEntry in entry.Finalizers)
            {
                DoUnpatch(patchEntry);
            }
        }
    }
}