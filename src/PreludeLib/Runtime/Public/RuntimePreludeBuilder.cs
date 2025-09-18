using System.Reflection;
using HarmonyLib;
using PreludeLib.Runtime.Internal;
using PreludeLib.Runtime.Registry;

namespace PreludeLib.Runtime.Public;

public class RuntimePreludeBuilder(RuntimePrelude owner, string id, IRuntimePatchRegistry registry)
    : IRuntimeRegistryBuilder
{
    private readonly RuntimePrelude _owner = owner;
    private readonly RuntimeRegistryBuilder _builder = new(id, registry);

    public void ScanAndPatchAll(Assembly patchAssembly)
        => _builder.ScanAndPatchAll(patchAssembly);

    public void ScanAndPatchCategory(Assembly patchAssembly, string? category)
        => _builder.ScanAndPatchCategory(patchAssembly, category);

    public void ScanAndPatchUncategorized(Assembly patchAssembly)
        => _builder.ScanAndPatchUncategorized(patchAssembly);

    public void ScanAndPatch(Type containerType)
        => _builder.ScanAndPatch(containerType);

    public void Patch(
        PatchTarget target,
        HarmonyMethod? prefix = null,
        HarmonyMethod? postfix = null,
        HarmonyMethod? finalizer = null,
        HarmonyMethod? transpiler = null,
        PatchGroup group = default)
        => _builder.Patch(target, prefix, postfix, finalizer, transpiler, group);

    public void Patch(PatchTarget target, HarmonyPatchType patchType, HarmonyMethod patchMethod, PatchGroup group = default)
        => _builder.Patch(target, patchType, patchMethod, group);

    public void PatchPrefix(PatchTarget target, HarmonyMethod prefix, PatchGroup group = default)
        => _builder.PatchPrefix(target, prefix, group);

    public void PatchPostfix(PatchTarget target, HarmonyMethod prefix, PatchGroup group = default)
        => _builder.PatchPostfix(target, prefix, group);

    public void PatchFinalizer(PatchTarget target, HarmonyMethod prefix, PatchGroup group = default)
        => _builder.PatchFinalizer(target, prefix, group);

    public void UnpatchAll()
        => _builder.UnpatchAll();

    public void UnpatchAll(Assembly patchAssembly)
        => _builder.UnpatchAll(patchAssembly);

    public void UnpatchCategory(Assembly patchAssembly, string category)
        => _builder.UnpatchCategory(patchAssembly, category);

    public void UnpatchUncategorized(Assembly patchAssembly)
        => _builder.UnpatchUncategorized(patchAssembly);

    public void UnpatchCategory(string category)
        => _builder.UnpatchCategory(category);

    public void UnpatchUncategorized()
        => _builder.UnpatchUncategorized();

    public void Unpatch(PatchTarget target, HarmonyPatchType patchType)
        => _builder.Unpatch(target, patchType);

    public void Unpatch(PatchTarget target, MethodInfo patch)
        => _builder.Unpatch(target, patch);
}