extern alias OfficialCecil;
using HarmonyLib;
using Microsoft.Extensions.Logging;
using PreludeLib.Common;
using OfficialCecil::Mono.Cecil;
using PreludeLib.Common;
using PreludeLib.CompileTime.Backend;
using PreludeLib.CompileTime.Internal;
using PreludeLib.CompileTime.Registry;

namespace PreludeLib.CompileTime.Public;

public class CompileTimePrelude : ICompileTimeRegistryBuilder
{
    private readonly CompileTimePatchRegistry _registry;
    private readonly CompileTimeRegistryBuilder _builder;
    private readonly ICompileTimeBackend _backend;
    
    public ICompileTimeBackend Backend => _backend;
    
    public CompileTimePrelude(ICompileTimeBackend backend, ILogger logger)
    {
        _backend = backend;

        _registry = new CompileTimePatchRegistry();
        _builder = new CompileTimeRegistryBuilder(_registry, logger);
    }

    public void ScanAndPatchAll(AssemblyDefinition patchAssemblyDef)
        => _builder.ScanAndPatchAll(patchAssemblyDef);

    public void ScanAndPatchCategory(AssemblyDefinition patchAssemblyDef, Category category)
        => _builder.ScanAndPatchCategory(patchAssemblyDef, category);

    public void ScanAndPatchUncategorized(AssemblyDefinition patchAssemblyDef)
        => _builder.ScanAndPatchUncategorized(patchAssemblyDef);

    public void ScanAndPatch(TypeReference containerTypeRef)
        => _builder.ScanAndPatch(containerTypeRef);

    public void ScanAndPatch(TypeDefinition containerTypeDef)
        => _builder.ScanAndPatch(containerTypeDef);

    public void Patch(
        CompileTimePatchTarget target,
        CompileTimePreludeMethod? prefix = null,
        CompileTimePreludeMethod? postfix = null,
        CompileTimePreludeMethod? finalizer = null,
        CompileTimePreludeMethod? transpiler = null)
        => _builder.Patch(target, prefix, postfix, finalizer, transpiler);

    public void Patch(CompileTimePatchTarget target, CompileTimeAttributePatch patch)
        => _builder.Patch(target, patch);

    public void Patch(CompileTimePatchTarget target, HarmonyPatchType patchType, CompileTimePreludeMethod patchMethod)
        => _builder.Patch(target, patchType, patchMethod);

    public void PatchPrefix(CompileTimePatchTarget target, CompileTimePreludeMethod prefix)
        => _builder.PatchPrefix(target, prefix);

    public void PatchPostfix(CompileTimePatchTarget target, CompileTimePreludeMethod postfix)
        => _builder.PatchPostfix(target, postfix);

    public void PatchFinalizer(CompileTimePatchTarget target, CompileTimePreludeMethod finalizer)
        => _builder.PatchFinalizer(target, finalizer);

    public void Commit()
    {
        _backend.Commit(_registry);
        _registry.ResetChanges();
    }
}