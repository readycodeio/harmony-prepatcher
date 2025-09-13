using HarmonyLib;
using Mono.Cecil;
using PreludeLib.CompileTime.Public;

namespace PreludeLib.CompileTime.Registry;

public interface ICompileTimePatchRegistry
{
    IEnumerable<MethodDefinition> GetOriginalMethods();
    IEnumerable<CompileTimePreludeMethod> GetPatchMethods(MethodDefinition original, HarmonyPatchType patchType);
    IEnumerable<CompileTimePreludeMethod> GetPrefixMethods(MethodDefinition original);
    IEnumerable<CompileTimePreludeMethod> GetPostfixMethods(MethodDefinition original);
    IEnumerable<CompileTimePreludeMethod> GetFinalizerMethods(MethodDefinition original);
    
    void AddOriginalMethod(MethodReference originalRef);
    void AddOriginalMethod(MethodDefinition originalDef);
    void AddPatchMethod(MethodReference original, HarmonyPatchType patchType, CompileTimePreludeMethod patchMethod);
    void AddPatchMethod(MethodDefinition originalDef, HarmonyPatchType patchType, CompileTimePreludeMethod patchMethod);
    void AddPatchMethod(MethodDefinition originalDef, CompileTimePreludePatch patchInfo);
}