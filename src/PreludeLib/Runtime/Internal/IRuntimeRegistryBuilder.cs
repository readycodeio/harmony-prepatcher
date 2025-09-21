using System.Reflection;
using HarmonyLib;
using PreludeLib.Common;
using PreludeLib.Runtime.Registry;

namespace PreludeLib.Runtime.Internal;

internal interface IRuntimeRegistryBuilder
{
    void ScanAndPatchAll();
    void ScanAndPatchAll(Assembly patchAssembly);
    void ScanAndPatchAllCalling();
    void ScanAndPatchCategory(Category category);
    void ScanAndPatchCategory(Assembly patchAssembly, Category category);
    void ScanAndPatchCategoryCalling(Category category);
    void ScanAndPatchUncategorized();
    void ScanAndPatchUncategorized(Assembly patchAssembly);
    void ScanAndPatchUncategorizedCalling();
    void ScanAndPatch(Type containerType);
    
    void Patch(
        PatchTarget target,
        HarmonyMethod? prefix = null,
        HarmonyMethod? postfix = null,
        HarmonyMethod? finalizer = null,
        HarmonyMethod? transpiler = null
    );
    void Patch(
        MethodBase original,
        HarmonyMethod? prefix = null,
        HarmonyMethod? postfix = null,
        HarmonyMethod? finalizer = null,
        HarmonyMethod? transpiler = null,
        PatchGroup group = default
    );
    void Patch(PatchTarget target, HarmonyPatchType patchType, HarmonyMethod patchMethod);
    void Patch(MethodBase original, HarmonyPatchType patchType, HarmonyMethod patchMethod, PatchGroup group = default);
    void PatchPrefix(PatchTarget target, HarmonyMethod prefix);
    void PatchPrefix(MethodBase original, HarmonyMethod prefix, PatchGroup group = default);
    void PatchPostfix(PatchTarget target, HarmonyMethod postfix);
    void PatchPostfix(MethodBase original, HarmonyMethod postfix, PatchGroup group = default);
    void PatchFinalizer(PatchTarget target, HarmonyMethod finalizer);
    void PatchFinalizer(MethodBase original, HarmonyMethod finalizer, PatchGroup group = default);
    
    void UnpatchAll();
    void UnpatchAll(Assembly patchAssembly);
    void UnpatchCategory(Assembly patchAssembly, Category category);
    void UnpatchUncategorized(Assembly patchAssembly);
    void UnpatchCategory(Category category);
    void UnpatchUncategorized();
    void Unpatch(PatchTarget target, HarmonyPatchType patchType);
    void Unpatch(MethodBase original, HarmonyPatchType patchType, PatchGroup group = default);
    void Unpatch(PatchTarget target, MethodInfo patch);
    void Unpatch(MethodBase original, MethodInfo patch, PatchGroup group = default);
}
