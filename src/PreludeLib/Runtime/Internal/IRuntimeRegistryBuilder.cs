using System.Reflection;
using HarmonyLib;
using PreludeLib.Runtime.Registry;

namespace PreludeLib.Runtime.Internal;

internal interface IRuntimeRegistryBuilder
{
    void ScanAndPatchAll(Assembly patchAssembly);
    void ScanAndPatchCategory(Assembly patchAssembly, string? category);
    void ScanAndPatchUncategorized(Assembly patchAssembly);
    void ScanAndPatch(Type containerType);
    
    void Patch(
        PatchTarget target,
        HarmonyMethod? prefix = null,
        HarmonyMethod? postfix = null,
        HarmonyMethod? finalizer = null,
        HarmonyMethod? transpiler = null,
        PatchGroup group = default
    );
    void Patch(PatchTarget target, HarmonyPatchType patchType, HarmonyMethod patchMethod, PatchGroup group = default);
    void PatchPrefix(PatchTarget target, HarmonyMethod prefix, PatchGroup group = default);
    void PatchPostfix(PatchTarget target, HarmonyMethod prefix, PatchGroup group = default);
    void PatchFinalizer(PatchTarget target, HarmonyMethod prefix, PatchGroup group = default);
    
    void UnpatchAll();
    void UnpatchAll(Assembly patchAssembly);
    void UnpatchCategory(Assembly patchAssembly, string category);
    void UnpatchUncategorized(Assembly patchAssembly);
    void UnpatchCategory(string category);
    void UnpatchUncategorized();
    void Unpatch(PatchTarget target, HarmonyPatchType patchType);
    void Unpatch(PatchTarget target, MethodInfo patch);
}
