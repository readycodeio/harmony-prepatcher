using System.Reflection;
using HarmonyLib;

namespace PreludeLib.Runtime.Internal;

internal interface IRuntimeRegistryBuilder
{
    void ScanAndPatchAll(Assembly patchAssembly);
    void ScanAndPatchCategory(Assembly patchAssembly, string? category);
    void ScanAndPatchUncategorized(Assembly patchAssembly);
    void ScanAndPatch(Type containerType);
    
    void Patch(
        MethodBase original,
        HarmonyMethod? prefix = null,
        HarmonyMethod? postfix = null,
        HarmonyMethod? finalizer = null,
        HarmonyMethod? transpiler = null
    );
    void Patch(MethodBase original, HarmonyPatchType patchType, HarmonyMethod patchMethod);
    void PatchPrefix(MethodBase original, HarmonyMethod prefix);
    void PatchPostfix(MethodBase original, HarmonyMethod prefix);
    void PatchFinalizer(MethodBase original, HarmonyMethod prefix);
    
    void UnpatchAll();
    void UnpatchAll(Assembly patchAssembly);
    void UnpatchCategory(Assembly patchAssembly, string category);
    void UnpatchUncategorized(Assembly patchAssembly);
    void UnpatchCategory(string category);
    void UnpatchUncategorized();
    void Unpatch(MethodBase original, HarmonyPatchType patchType);
    void Unpatch(MethodBase original, MethodInfo patch);
}
