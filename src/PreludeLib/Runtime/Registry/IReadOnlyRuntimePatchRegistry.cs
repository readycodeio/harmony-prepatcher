using System.Reflection;
using HarmonyLib;
using PreludeLib.Common;

namespace PreludeLib.Runtime.Registry;

public interface IReadOnlyRuntimePatchRegistry
{
    IEnumerable<MethodBase> GetOriginalMethods();
    IEnumerable<MethodBase> GetAddedOriginalMethods();
    bool HasOriginalMethod(MethodBase original);
    bool HasAddedOriginalMethod(MethodBase original);

    IEnumerable<string> GetIds();
    IEnumerable<string> GetAddedIds();
    bool HasId(string id);
    bool HasAddedId(string id);
    
    IEnumerable<HarmonyMethod> GetPatchMethods(MethodBase original, string? id, HarmonyPatchType patchType);
    IEnumerable<HarmonyMethod> GetPrefixMethods(MethodBase original, string? id);
    IEnumerable<HarmonyMethod> GetPostfixMethods(MethodBase original, string? id);
    IEnumerable<HarmonyMethod> GetFinalizerMethods(MethodBase original, string? id);
    
    IEnumerable<HarmonyMethod> GetCategoryPatchMethods(MethodBase original, string? id, HarmonyPatchType patchType, Category category);
    IEnumerable<HarmonyMethod> GetCategoryPrefixMethods(MethodBase original, string? id, Category category);
    IEnumerable<HarmonyMethod> GetCategoryPostfixMethods(MethodBase original, string? id, Category category);
    IEnumerable<HarmonyMethod> GetCategoryFinalizerMethods(MethodBase original, string? id, Category category);

    IEnumerable<HarmonyMethod> GetUncategorizedPatchMethods(MethodBase original, string? id, HarmonyPatchType patchType);
    IEnumerable<HarmonyMethod> GetUncategorizedPrefixMethods(MethodBase original, string? id);
    IEnumerable<HarmonyMethod> GetUncategorizedPostfixMethods(MethodBase original, string? id);
    IEnumerable<HarmonyMethod> GetUncategorizedFinalizerMethods(MethodBase original, string? id);
    
    IEnumerable<HarmonyMethod> GetAddedPatchMethods(MethodBase original, string? id, HarmonyPatchType patchType);
    IEnumerable<HarmonyMethod> GetAddedPrefixMethods(MethodBase original, string? id);
    IEnumerable<HarmonyMethod> GetAddedPostfixMethods(MethodBase original, string? id);
    IEnumerable<HarmonyMethod> GetAddedFinalizerMethods(MethodBase original, string? id);
    bool HasAddedPatchMethod(MethodBase original, string? id, HarmonyPatchType patchType);
    
    IEnumerable<HarmonyMethod> GetRemovedPatchMethods(MethodBase original, string? id, HarmonyPatchType patchType);
    IEnumerable<HarmonyMethod> GetRemovedPrefixMethods(MethodBase original, string? id);
    IEnumerable<HarmonyMethod> GetRemovedPostfixMethods(MethodBase original, string? id);
    IEnumerable<HarmonyMethod> GetRemovedFinalizerMethods(MethodBase original, string? id);
    bool HasRemovedPatchMethod(MethodBase original, string? id, HarmonyPatchType patchType);
}