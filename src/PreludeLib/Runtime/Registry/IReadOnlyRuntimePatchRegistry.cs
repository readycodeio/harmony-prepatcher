using System.Reflection;
using HarmonyLib;
using PreludeLib.Common;

namespace PreludeLib.Runtime.Registry;

public interface IReadOnlyRuntimePatchRegistry
{
    IEnumerable<PatchGroup> GetGroups();
    bool HasGroup(PatchGroup group);
    
    IEnumerable<PatchTarget> GetTargets(PatchGroup group);
    IEnumerable<PatchTarget> GetAddedTargets(PatchGroup group);
    bool HasTarget(PatchGroup group, PatchTarget target);
    bool HasAddedTarget(PatchGroup group, PatchTarget target);

    IEnumerable<string> GetIds();
    IEnumerable<string> GetAddedIds();
    bool HasId(string id);
    bool HasAddedId(string id);
    
    IEnumerable<HarmonyMethod> GetPatchMethods(PatchTarget target, string? id, HarmonyPatchType patchType);
    IEnumerable<HarmonyMethod> GetPrefixMethods(PatchTarget target, string? id);
    IEnumerable<HarmonyMethod> GetPostfixMethods(PatchTarget target, string? id);
    IEnumerable<HarmonyMethod> GetFinalizerMethods(PatchTarget target, string? id);
    
    IEnumerable<HarmonyMethod> GetCategoryPatchMethods(PatchTarget target, string? id, HarmonyPatchType patchType, Category category);
    IEnumerable<HarmonyMethod> GetCategoryPrefixMethods(PatchTarget target, string? id, Category category);
    IEnumerable<HarmonyMethod> GetCategoryPostfixMethods(PatchTarget target, string? id, Category category);
    IEnumerable<HarmonyMethod> GetCategoryFinalizerMethods(PatchTarget target, string? id, Category category);

    IEnumerable<HarmonyMethod> GetUncategorizedPatchMethods(PatchTarget target, string? id, HarmonyPatchType patchType);
    IEnumerable<HarmonyMethod> GetUncategorizedPrefixMethods(PatchTarget target, string? id);
    IEnumerable<HarmonyMethod> GetUncategorizedPostfixMethods(PatchTarget target, string? id);
    IEnumerable<HarmonyMethod> GetUncategorizedFinalizerMethods(PatchTarget target, string? id);
    
    IEnumerable<HarmonyMethod> GetAddedPatchMethods(PatchTarget target, string? id, HarmonyPatchType patchType);
    IEnumerable<HarmonyMethod> GetAddedPrefixMethods(PatchTarget target, string? id);
    IEnumerable<HarmonyMethod> GetAddedPostfixMethods(PatchTarget target, string? id);
    IEnumerable<HarmonyMethod> GetAddedFinalizerMethods(PatchTarget target, string? id);
    bool HasAddedPatchMethod(PatchTarget target, string? id, HarmonyPatchType patchType);
    
    IEnumerable<HarmonyMethod> GetRemovedPatchMethods(PatchTarget target, string? id, HarmonyPatchType patchType);
    IEnumerable<HarmonyMethod> GetRemovedPrefixMethods(PatchTarget target, string? id);
    IEnumerable<HarmonyMethod> GetRemovedPostfixMethods(PatchTarget target, string? id);
    IEnumerable<HarmonyMethod> GetRemovedFinalizerMethods(PatchTarget target, string? id);
    bool HasRemovedPatchMethod(PatchTarget target, string? id, HarmonyPatchType patchType);
    
    MethodInfo? GetPrepareGroupCallback(PatchGroup group);
    MethodInfo? GetCleanupGroupCallback(PatchGroup group);
    MethodInfo? GetPreparePatchMethodCallback(HarmonyMethod patchMethod);
    MethodInfo? GetCleanupPatchMethodCallback(HarmonyMethod patchMethod);
}