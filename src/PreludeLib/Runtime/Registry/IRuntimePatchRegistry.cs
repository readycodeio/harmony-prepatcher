using System.Reflection;
using HarmonyLib;

namespace PreludeLib.Runtime.Registry;

public interface IRuntimePatchRegistry : IReadOnlyRuntimePatchRegistry
{
    void AddGroup(PatchGroup group);
    void AddTarget(PatchGroup group, PatchTarget target);
    
    void AddPatchMethod(PatchTarget target, string id, HarmonyPatchType patchType, HarmonyMethod patchMethod);
    
    void RemovePatchMethod(PatchTarget target, string? id, HarmonyPatchType patchType);
    void RemovePatchMethod(PatchTarget target, string? id, HarmonyMethod patchMethod);
    void RemovePatchMethod(PatchTarget target, string? id, MethodInfo patchMethod);

    void SetPrepareGroupCallback(PatchGroup group, MethodInfo? callback);
    void SetCleanupGroupCallback(PatchGroup group, MethodInfo? callback);
    void SetPreparePatchMethodCallback(HarmonyMethod patchMethod, MethodInfo? callback);
    void SetCleanupPatchMethodCallback(HarmonyMethod patchMethod, MethodInfo? callback);
    
    void ResetChanges();
}
