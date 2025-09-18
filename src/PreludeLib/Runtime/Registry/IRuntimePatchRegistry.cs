using System.Reflection;
using HarmonyLib;

namespace PreludeLib.Runtime.Registry;

public interface IRuntimePatchRegistry : IReadOnlyRuntimePatchRegistry
{
    void AddOriginalMethod(MethodBase original);
    void AddContainerType(Type type);
    
    void AddPatchMethod(MethodBase original, string id, HarmonyPatchType patchType, HarmonyMethod patchMethod);
    
    void RemovePatchMethod(MethodBase original, string? id, HarmonyPatchType patchType);
    void RemovePatchMethod(MethodBase original, string? id, HarmonyMethod patchMethod);
    void RemovePatchMethod(MethodBase original, string? id, MethodInfo patchMethod);

    void ResetChanges();
}
