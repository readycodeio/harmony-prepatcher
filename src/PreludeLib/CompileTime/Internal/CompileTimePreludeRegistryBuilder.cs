using HarmonyLib;
using Mono.Cecil;
using PreludeLib.CompileTime.Registry;

namespace PreludeLib.CompileTime.Public;

internal class CompileTimePreludeRegistryBuilder(ICompileTimePatchRegistry registry) : ICompileTimePreludeRegistryBuilder
{
    public void PatchAdd(
        MethodDefinition originalDef,
        CompileTimePreludeMethod? prefix = null,
        CompileTimePreludeMethod? postfix = null,
        CompileTimePreludeMethod? finalizer = null,
        CompileTimePreludeMethod? transpiler = null
    )
    {
        if (prefix != null)
            PatchAdd(originalDef, HarmonyPatchType.Prefix, prefix);
        if (postfix != null)
            PatchAdd(originalDef, HarmonyPatchType.Postfix, postfix);
        if (finalizer != null)
            PatchAdd(originalDef, HarmonyPatchType.Finalizer, finalizer);
        if (transpiler != null)
            throw new NotSupportedException("Transpilers are not supported.");
        // processor.AddInfix(infix);
    }
    
    public void PatchAdd(MethodReference originalDef, CompileTimePreludePatch patch)
    {
        PatchAdd(originalDef, patch.PatchType, patch.PatchMethod);
    }

    public void PatchAdd(MethodReference originalDef, HarmonyPatchType patchType, CompileTimePreludeMethod patchMethod)
    {
        registry.AddOriginalMethod(originalDef);
        registry.AddPatchMethod(originalDef, HarmonyPatchType.Postfix, patchMethod);
    }

    public void PatchAddPrefix(MethodReference originalDef, CompileTimePreludeMethod prefix)
        => PatchAdd(originalDef, HarmonyPatchType.Prefix, prefix);

    public void PatchAddPostfix(MethodReference originalDef, CompileTimePreludeMethod prefix)
        => PatchAdd(originalDef, HarmonyPatchType.Postfix, prefix);

    public void PatchAddFinalizer(MethodReference originalDef, CompileTimePreludeMethod prefix)
        => PatchAdd(originalDef, HarmonyPatchType.Finalizer, prefix);
}
