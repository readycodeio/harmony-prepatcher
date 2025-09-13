using HarmonyLib;
using Mono.Cecil;

namespace PreludeLib.CompileTime.Public;

internal interface ICompileTimePreludeRegistryBuilder
{
    void PatchAdd(
        MethodDefinition originalDef,
        CompileTimePreludeMethod? prefix = null,
        CompileTimePreludeMethod? postfix = null,
        CompileTimePreludeMethod? finalizer = null,
        CompileTimePreludeMethod? transpiler = null
    );
    void PatchAdd(MethodReference originalDef, CompileTimePreludePatch patch);
    void PatchAdd(MethodReference originalDef, HarmonyPatchType patchType, CompileTimePreludeMethod patchMethod);
    void PatchAddPrefix(MethodReference originalDef, CompileTimePreludeMethod prefix);
    void PatchAddPostfix(MethodReference originalDef, CompileTimePreludeMethod prefix);
    void PatchAddFinalizer(MethodReference originalDef, CompileTimePreludeMethod prefix);
}