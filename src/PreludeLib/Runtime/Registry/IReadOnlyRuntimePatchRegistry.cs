using System.Reflection;
using HarmonyLib;
using PreludeLib.Common;

namespace PreludeLib.Runtime.Registry;

public interface IReadOnlyRuntimePatchRegistry
{
    IEnumerable<string> GetIds();
    bool HasId(string id);
    
    IEnumerable<PatchGroup> GetGroups(string id);
    bool HasGroup(PatchGroup group, string id);
    
    IEnumerable<PatchTarget> GetTargets(string id);
    IEnumerable<PatchTarget> GetTargets(PatchGroup group, string id);
    IEnumerable<PatchTarget> GetAddedTargets(string id);
    IEnumerable<PatchTarget> GetAddedTargets(PatchGroup group, string id);
    bool HasTarget(PatchTarget target, string id);
    bool HasAddedTarget(PatchTarget target, string id);
    
    IEnumerable<HarmonyMethod> GetPatchMethods(PatchTarget target, string id, HarmonyPatchType patchType);
    IEnumerable<HarmonyMethod> GetPrefixMethods(PatchTarget target, string id);
    IEnumerable<HarmonyMethod> GetPostfixMethods(PatchTarget target, string id);
    IEnumerable<HarmonyMethod> GetFinalizerMethods(PatchTarget target, string id);
    
    IEnumerable<HarmonyMethod> GetCategoryPatchMethods(PatchTarget target, string id, HarmonyPatchType patchType, Category category);
    IEnumerable<HarmonyMethod> GetCategoryPrefixMethods(PatchTarget target, string id, Category category);
    IEnumerable<HarmonyMethod> GetCategoryPostfixMethods(PatchTarget target, string id, Category category);
    IEnumerable<HarmonyMethod> GetCategoryFinalizerMethods(PatchTarget target, string id, Category category);

    IEnumerable<HarmonyMethod> GetUncategorizedPatchMethods(PatchTarget target, string id, HarmonyPatchType patchType);
    IEnumerable<HarmonyMethod> GetUncategorizedPrefixMethods(PatchTarget target, string id);
    IEnumerable<HarmonyMethod> GetUncategorizedPostfixMethods(PatchTarget target, string id);
    IEnumerable<HarmonyMethod> GetUncategorizedFinalizerMethods(PatchTarget target, string id);
    
    IEnumerable<HarmonyMethod> GetAddedPatchMethods(PatchTarget target, string id, HarmonyPatchType patchType);
    IEnumerable<HarmonyMethod> GetAddedPrefixMethods(PatchTarget target, string id);
    IEnumerable<HarmonyMethod> GetAddedPostfixMethods(PatchTarget target, string id);
    IEnumerable<HarmonyMethod> GetAddedFinalizerMethods(PatchTarget target, string id);
    bool HasAddedPatchMethod(PatchTarget target, string id, HarmonyPatchType patchType);
    
    IEnumerable<HarmonyMethod> GetRemovedPatchMethods(PatchTarget target, string id, HarmonyPatchType patchType);
    IEnumerable<HarmonyMethod> GetRemovedPrefixMethods(PatchTarget target, string id);
    IEnumerable<HarmonyMethod> GetRemovedPostfixMethods(PatchTarget target, string id);
    IEnumerable<HarmonyMethod> GetRemovedFinalizerMethods(PatchTarget target, string id);
    bool HasRemovedPatchMethod(PatchTarget target, string id, HarmonyPatchType patchType);
    
    MethodInfo? GetPrepareGroupCallback(PatchGroup group, string id);
    MethodInfo? GetCleanupGroupCallback(PatchGroup group, string id);
    MethodInfo? GetPreparePatchMethodCallback(HarmonyMethod patchMethod, string id);
    MethodInfo? GetCleanupPatchMethodCallback(HarmonyMethod patchMethod, string id);
}