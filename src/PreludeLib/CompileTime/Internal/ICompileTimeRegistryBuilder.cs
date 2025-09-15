using HarmonyLib;
using Mono.Cecil;
using PreludeLib.CompileTime.Public;

namespace PreludeLib.CompileTime.Internal;

internal interface ICompileTimeRegistryBuilder
{
    void ScanAndPatchAll(AssemblyDefinition patchAssemblyDef);
    void ScanAndPatchCategory(AssemblyDefinition patchAssemblyDef, string? category);
    void ScanAndPatchUncategorized(AssemblyDefinition patchAssemblyDef);
    void ScanAndPatch(TypeReference containerTypeRef);
    void ScanAndPatch(TypeDefinition containerTypeDef);
    
    void Patch(
        MethodDefinition originalDef,
        CompileTimePreludeMethod? prefix = null,
        CompileTimePreludeMethod? postfix = null,
        CompileTimePreludeMethod? finalizer = null,
        CompileTimePreludeMethod? transpiler = null
    );
    void Patch(MethodReference originalDef, CompileTimePreludePatch patch);
    void Patch(MethodReference originalDef, HarmonyPatchType patchType, CompileTimePreludeMethod patchMethod);
    void PatchPrefix(MethodReference originalDef, CompileTimePreludeMethod prefix);
    void PatchPostfix(MethodReference originalDef, CompileTimePreludeMethod prefix);
    void PatchFinalizer(MethodReference originalDef, CompileTimePreludeMethod prefix);
}