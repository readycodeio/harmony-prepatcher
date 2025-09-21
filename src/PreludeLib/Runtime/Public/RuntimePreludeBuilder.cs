using System.Reflection;
using HarmonyLib;
using PreludeLib.Common;
using PreludeLib.Runtime.Internal;
using PreludeLib.Runtime.Registry;

namespace PreludeLib.Runtime.Public;

public class RuntimePreludeBuilder(RuntimePrelude owner, string id, IRuntimePatchRegistry registry)
    : IRuntimeRegistryBuilder
{
    private readonly RuntimePrelude _owner = owner;
    private readonly RuntimeRegistryBuilder _builder = new(id, registry);

    public void ScanAndPatchAll()
        => _builder.ScanAndPatchAll();

    public void ScanAndPatchAll(Assembly patchAssembly)
        => _builder.ScanAndPatchAll(patchAssembly);

    public void ScanAndPatchAllCalling()
        => _builder.ScanAndPatchAllCalling();

    public void ScanAndPatchCategory(Category category)
        => _builder.ScanAndPatchCategory(category);

    public void ScanAndPatchCategory(Assembly patchAssembly, Category category)
        => _builder.ScanAndPatchCategory(patchAssembly, category);

    public void ScanAndPatchCategoryCalling(Category category)
        => _builder.ScanAndPatchCategoryCalling(category);

    public void ScanAndPatchUncategorized()
        => _builder.ScanAndPatchUncategorized();

    public void ScanAndPatchUncategorized(Assembly patchAssembly)
        => _builder.ScanAndPatchUncategorized(patchAssembly);

    public void ScanAndPatchUncategorizedCalling()
        => _builder.ScanAndPatchUncategorizedCalling();

    public void ScanAndPatch(Type containerType)
        => _builder.ScanAndPatch(containerType);

    public void Patch(
        PatchTarget target,
        HarmonyMethod? prefix = null,
        HarmonyMethod? postfix = null,
        HarmonyMethod? finalizer = null,
        HarmonyMethod? transpiler = null)
        => _builder.Patch(target, prefix, postfix, finalizer, transpiler);

    public void Patch(
        MethodBase original,
        HarmonyMethod? prefix = null,
        HarmonyMethod? postfix = null,
        HarmonyMethod? finalizer = null,
        HarmonyMethod? transpiler = null,
        PatchGroup group = default)
        => _builder.Patch(original, prefix, postfix, finalizer, transpiler, group);

    public void Patch(PatchTarget target, HarmonyPatchType patchType, HarmonyMethod patchMethod)
        => _builder.Patch(target, patchType, patchMethod);
    
    public void Patch(MethodBase original, HarmonyPatchType patchType, HarmonyMethod patchMethod, PatchGroup group = default)
        => _builder.Patch(original, patchType, patchMethod, group);

    public void PatchPrefix(PatchTarget target, HarmonyMethod prefix)
        => _builder.PatchPrefix(target, prefix);

    public void PatchPrefix(MethodBase original, HarmonyMethod prefix, PatchGroup group = default)
        => _builder.PatchPrefix(original, prefix, group);

    public void PatchPostfix(PatchTarget target, HarmonyMethod postfix)
        => _builder.PatchPostfix(target, postfix);

    public void PatchPostfix(MethodBase original, HarmonyMethod postfix, PatchGroup group = default)
        => _builder.PatchPostfix(original, postfix, group);

    public void PatchFinalizer(PatchTarget target, HarmonyMethod finalizer)
        => _builder.PatchFinalizer(target, finalizer);

    public void PatchFinalizer(MethodBase original, HarmonyMethod finalizer, PatchGroup group = default)
        => _builder.PatchFinalizer(original, finalizer, group);

    public void UnpatchAll()
        => _builder.UnpatchAll();

    public void UnpatchAll(Assembly patchAssembly)
        => _builder.UnpatchAll(patchAssembly);

    public void UnpatchCategory(Assembly patchAssembly, Category category)
        => _builder.UnpatchCategory(patchAssembly, category);

    public void UnpatchUncategorized(Assembly patchAssembly)
        => _builder.UnpatchUncategorized(patchAssembly);

    public void UnpatchCategory(Category category)
        => _builder.UnpatchCategory(category);

    public void UnpatchUncategorized()
        => _builder.UnpatchUncategorized();

    public void Unpatch(PatchTarget target, HarmonyPatchType patchType)
        => _builder.Unpatch(target, patchType);

    public void Unpatch(MethodBase original, HarmonyPatchType patchType, PatchGroup group)
        => _builder.Unpatch(PatchTarget.FromOriginal(original, group), patchType);

    public void Unpatch(PatchTarget target, MethodInfo patch)
        => _builder.Unpatch(target, patch);

    public void Unpatch(MethodBase original, MethodInfo patch, PatchGroup group)
        => _builder.Unpatch(PatchTarget.FromOriginal(original, group), patch);
}