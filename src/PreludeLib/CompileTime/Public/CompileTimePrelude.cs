using HarmonyLib;
using Microsoft.Extensions.Logging;
using Mono.Cecil;
using PreludeLib.CompileTime.Backend;
using PreludeLib.CompileTime.Internal;
using PreludeLib.CompileTime.Registry;

namespace PreludeLib.CompileTime.Public;

public class CompileTimePrelude : ICompileTimeRegistryBuilder
{
    private readonly CompileTimePatchRegistry _registry;
    private readonly CompileTimeRegistryBuilder _builder;
    private readonly ICompileTimeBackend _backend;
    
    public CompileTimePrelude(ICompileTimeBackend backend, ILogger logger)
    {
        _backend = backend;

        _registry = new CompileTimePatchRegistry();
        _builder = new CompileTimeRegistryBuilder(_registry, logger);
    }

    public void ScanAndPatchAll(AssemblyDefinition patchAssemblyDef)
        => _builder.ScanAndPatchAll(patchAssemblyDef);

    public void ScanAndPatchCategory(AssemblyDefinition patchAssemblyDef, string category)
        => _builder.ScanAndPatchCategory(patchAssemblyDef, category);

    public void ScanAndPatchUncategorized(AssemblyDefinition patchAssemblyDef)
        => _builder.ScanAndPatchUncategorized(patchAssemblyDef);

    public void ScanAndPatch(TypeReference containerTypeRef)
        => _builder.ScanAndPatch(containerTypeRef);

    public void ScanAndPatch(TypeDefinition containerTypeDef)
        => _builder.ScanAndPatch(containerTypeDef);

    public void Patch(
        MethodDefinition originalDef,
        CompileTimePreludeMethod? prefix = null,
        CompileTimePreludeMethod? postfix = null,
        CompileTimePreludeMethod? finalizer = null,
        CompileTimePreludeMethod? transpiler = null)
        => _builder.Patch(originalDef, prefix, postfix, finalizer, transpiler);

    public void Patch(MethodReference originalDef, CompileTimePreludePatch patch)
        => _builder.Patch(originalDef, patch);

    public void Patch(MethodReference originalDef, HarmonyPatchType patchType, CompileTimePreludeMethod patchMethod)
        => _builder.Patch(originalDef, patchType, patchMethod);

    public void PatchPrefix(MethodReference originalDef, CompileTimePreludeMethod prefix)
        => _builder.PatchPrefix(originalDef, prefix);

    public void PatchPostfix(MethodReference originalDef, CompileTimePreludeMethod prefix)
        => _builder.PatchPostfix(originalDef, prefix);

    public void PatchFinalizer(MethodReference originalDef, CompileTimePreludeMethod prefix)
        => _builder.PatchFinalizer(originalDef, prefix);

    public void Commit()
    {
        _backend.Commit(_registry);
        _registry.ResetChanges();
    }
}