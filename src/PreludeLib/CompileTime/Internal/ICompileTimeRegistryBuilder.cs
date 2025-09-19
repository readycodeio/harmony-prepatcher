using HarmonyLib;
using Mono.Cecil;
using PreludeLib.CompileTime.Public;
using PreludeLib.CompileTime.Registry;

namespace PreludeLib.CompileTime.Internal;

internal interface ICompileTimeRegistryBuilder
{
    void ScanAndPatchAll(AssemblyDefinition patchAssemblyDef);
    void ScanAndPatchCategory(AssemblyDefinition patchAssemblyDef, string? category);
    void ScanAndPatchUncategorized(AssemblyDefinition patchAssemblyDef);
    void ScanAndPatch(TypeReference containerTypeRef);
    void ScanAndPatch(TypeDefinition containerTypeDef);
    
    void Patch(
        CompileTimePatchTarget target,
        CompileTimePreludeMethod? prefix = null,
        CompileTimePreludeMethod? postfix = null,
        CompileTimePreludeMethod? finalizer = null,
        CompileTimePreludeMethod? transpiler = null
    );
    void Patch(CompileTimePatchTarget target, CompileTimeAttributePatch patch);
    void Patch(CompileTimePatchTarget target, HarmonyPatchType patchType, CompileTimePreludeMethod patchMethod);
    void PatchPrefix(CompileTimePatchTarget target, CompileTimePreludeMethod prefix);
    void PatchPostfix(CompileTimePatchTarget target, CompileTimePreludeMethod postfix);
    void PatchFinalizer(CompileTimePatchTarget target, CompileTimePreludeMethod finalizer);
}