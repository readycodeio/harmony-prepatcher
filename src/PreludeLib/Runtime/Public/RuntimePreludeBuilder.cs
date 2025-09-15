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
        MethodBase original,
        HarmonyMethod? prefix = null,
        HarmonyMethod? postfix = null,
        HarmonyMethod? finalizer = null,
        HarmonyMethod? transpiler = null)
        => _builder.Patch(original, prefix, postfix, finalizer, transpiler);

    public void Patch(MethodBase original, HarmonyPatchType patchType, HarmonyMethod patchMethod)
        => _builder.Patch(original, patchType, patchMethod);

    public void PatchPrefix(MethodBase original, HarmonyMethod prefix)
        => _builder.PatchPrefix(original, prefix);

    public void PatchPostfix(MethodBase original, HarmonyMethod prefix)
        => _builder.PatchPostfix(original, prefix);

    public void PatchFinalizer(MethodBase original, HarmonyMethod prefix)
        => _builder.PatchFinalizer(original, prefix);

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

    public void Unpatch(MethodBase original, HarmonyPatchType patchType)
        => _builder.Unpatch(original, patchType);

    public void Unpatch(MethodBase original, MethodInfo patch)
        => _builder.Unpatch(original, patch);
}