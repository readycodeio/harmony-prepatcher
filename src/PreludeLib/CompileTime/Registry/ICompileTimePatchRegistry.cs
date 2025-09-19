using HarmonyLib;
using PreludeLib.Common;
using PreludeLib.CompileTime.Public;

namespace PreludeLib.CompileTime.Registry;

public interface ICompileTimePatchRegistry
{
    IEnumerable<CompileTimePatchGroup> GetGroups();
    bool HasGroup(CompileTimePatchGroup group);
 
    IEnumerable<CompileTimePatchTarget> GetTargets();
    IEnumerable<CompileTimePatchTarget> GetAddedTargets();
    bool HasTarget(CompileTimePatchTarget target);
    bool HasAddedTarget(CompileTimePatchTarget target);
    
    IEnumerable<CompileTimePatchTarget> GetTargets(CompileTimePatchGroup group);
    IEnumerable<CompileTimePatchTarget> GetAddedTargets(CompileTimePatchGroup group);
    bool HasTarget(CompileTimePatchGroup group, CompileTimePatchTarget target);
    bool HasAddedTarget(CompileTimePatchGroup group, CompileTimePatchTarget target);
    
    IEnumerable<CompileTimePreludeMethod> GetPatchMethods(CompileTimePatchTarget target, HarmonyPatchType patchType);
    IEnumerable<CompileTimePreludeMethod> GetPrefixMethods(CompileTimePatchTarget target);
    IEnumerable<CompileTimePreludeMethod> GetPostfixMethods(CompileTimePatchTarget target);
    IEnumerable<CompileTimePreludeMethod> GetFinalizerMethods(CompileTimePatchTarget target);
    
    IEnumerable<CompileTimePreludeMethod> GetCategoryPatchMethods(CompileTimePatchTarget target, HarmonyPatchType patchType, Category category);
    IEnumerable<CompileTimePreludeMethod> GetCategoryPrefixMethods(CompileTimePatchTarget target, Category category);
    IEnumerable<CompileTimePreludeMethod> GetCategoryPostfixMethods(CompileTimePatchTarget target, Category category);
    IEnumerable<CompileTimePreludeMethod> GetCategoryFinalizerMethods(CompileTimePatchTarget target, Category category);
    
    IEnumerable<CompileTimePreludeMethod> GetUncategorizedPatchMethods(CompileTimePatchTarget target, HarmonyPatchType patchType);
    IEnumerable<CompileTimePreludeMethod> GetUncategorizedPrefixMethods(CompileTimePatchTarget target);
    IEnumerable<CompileTimePreludeMethod> GetUncategorizedPostfixMethods(CompileTimePatchTarget target);
    IEnumerable<CompileTimePreludeMethod> GetUncategorizedFinalizerMethods(CompileTimePatchTarget target);
    
    IEnumerable<CompileTimePreludeMethod> GetAddedPatchMethods(CompileTimePatchTarget target, HarmonyPatchType patchType);
    IEnumerable<CompileTimePreludeMethod> GetAddedPrefixMethods(CompileTimePatchTarget target);
    IEnumerable<CompileTimePreludeMethod> GetAddedPostfixMethods(CompileTimePatchTarget target);
    IEnumerable<CompileTimePreludeMethod> GetAddedFinalizerMethods(CompileTimePatchTarget target);
    bool HasAddedPatchMethod(CompileTimePatchTarget target, HarmonyPatchType patchType);

    void AddGroup(CompileTimePatchGroup group);
    void AddTarget(CompileTimePatchTarget target);
    void AddPatchMethod(CompileTimePatchTarget target, HarmonyPatchType patchType, CompileTimePreludeMethod patchMethod);
    void AddPatchMethod(CompileTimePatchTarget target, CompileTimeAttributePatch patchInfo);

    public void ResetChanges();
}