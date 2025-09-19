using System.Reflection;
using HarmonyLib;

namespace PreludeLib.Runtime.Registry;

public interface IRuntimePatchRegistry : IReadOnlyRuntimePatchRegistry
{
    void AddGroup(PatchGroup group, string id);
    void AddTarget(PatchTarget target, string id);
    void AddInstance(string id);
    
    void AddPatchMethod(PatchTarget target, string id, HarmonyPatchType patchType, HarmonyMethod patchMethod);
    
    void RemovePatchMethod(PatchTarget target, string id, HarmonyPatchType patchType);
    void RemovePatchMethod(PatchTarget target, string id, HarmonyMethod patchMethod);
    void RemovePatchMethod(PatchTarget target, string id, MethodInfo patchMethod);

    void SetPrepareGroupCallback(PatchGroup group, string id, MethodInfo? callback);
    void SetCleanupGroupCallback(PatchGroup group, string id, MethodInfo? callback);
    void SetPreparePatchMethodCallback(HarmonyMethod patchMethod, string id, MethodInfo? callback);
    void SetCleanupPatchMethodCallback(HarmonyMethod patchMethod, string id, MethodInfo? callback);
    
    void ResetChanges();
}
