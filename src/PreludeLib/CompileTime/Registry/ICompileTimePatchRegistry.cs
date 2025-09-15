using HarmonyLib;
using Mono.Cecil;
using PreludeLib.Common;
using PreludeLib.CompileTime.Public;

namespace PreludeLib.CompileTime.Registry;

public interface ICompileTimePatchRegistry
{
    IEnumerable<MethodDefinition> GetOriginalMethods();
    IEnumerable<MethodDefinition> GetAddedOriginalMethods();
    bool HasOriginalMethod(MethodDefinition originalDef);
    bool HasAddedOriginalMethod(MethodDefinition originalDef);
    
    IEnumerable<CompileTimePreludeMethod> GetPatchMethods(MethodDefinition originalDef, HarmonyPatchType patchType);
    IEnumerable<CompileTimePreludeMethod> GetPrefixMethods(MethodDefinition originalDef);
    IEnumerable<CompileTimePreludeMethod> GetPostfixMethods(MethodDefinition originalDef);
    IEnumerable<CompileTimePreludeMethod> GetFinalizerMethods(MethodDefinition originalDef);
    
    IEnumerable<CompileTimePreludeMethod> GetCategoryPatchMethods(MethodDefinition originalDef, HarmonyPatchType patchType, Category category);
    IEnumerable<CompileTimePreludeMethod> GetCategoryPrefixMethods(MethodDefinition originalDef, Category category);
    IEnumerable<CompileTimePreludeMethod> GetCategoryPostfixMethods(MethodDefinition originalDef, Category category);
    IEnumerable<CompileTimePreludeMethod> GetCategoryFinalizerMethods(MethodDefinition originalDef, Category category);
    
    IEnumerable<CompileTimePreludeMethod> GetUncategorizedPatchMethods(MethodDefinition originalDef, HarmonyPatchType patchType);
    IEnumerable<CompileTimePreludeMethod> GetUncategorizedPrefixMethods(MethodDefinition originalDef);
    IEnumerable<CompileTimePreludeMethod> GetUncategorizedPostfixMethods(MethodDefinition originalDef);
    IEnumerable<CompileTimePreludeMethod> GetUncategorizedFinalizerMethods(MethodDefinition originalDef);
    
    IEnumerable<CompileTimePreludeMethod> GetAddedPatchMethods(MethodDefinition originalDef, HarmonyPatchType patchType);
    IEnumerable<CompileTimePreludeMethod> GetAddedPrefixMethods(MethodDefinition originalDef);
    IEnumerable<CompileTimePreludeMethod> GetAddedPostfixMethods(MethodDefinition originalDef);
    IEnumerable<CompileTimePreludeMethod> GetAddedFinalizerMethods(MethodDefinition originalDef);
    bool HasAddedPatchMethod(MethodDefinition originalDef , HarmonyPatchType patchType);
    
    void AddOriginalMethod(MethodReference originalRef);
    void AddOriginalMethod(MethodDefinition originalDef);
    void AddPatchMethod(MethodReference originalRef, HarmonyPatchType patchType, CompileTimePreludeMethod patchMethod);
    void AddPatchMethod(MethodDefinition originalDef, HarmonyPatchType patchType, CompileTimePreludeMethod patchMethod);
    void AddPatchMethod(MethodReference originalRef, CompileTimePreludePatch patchInfo);
    void AddPatchMethod(MethodDefinition originalDef, CompileTimePreludePatch patchInfo);

    public void ResetChanges();
}