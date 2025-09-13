using HarmonyLib;
using Mono.Cecil;
using PreludeLib.CompileTime.Backend;
using PreludeLib.CompileTime.Registry;

namespace PreludeLib.CompileTime.Public;

public class CompileTimePrelude : ICompileTimePreludeAttributeScanner, ICompileTimePreludeRegistryBuilder
{
    private readonly CompileTimePatchRegistry _registry;
    private readonly CompileTimePreludeRegistryBuilder _registryBuilder;
    private readonly CompileTimePreludeAttributeScanner _scanner;
    private readonly ICompileTimePreludeBackend _backend;
    
    public CompileTimePrelude(ICompileTimePreludeBackend backend)
    {
        _backend = backend;

        _registry = new CompileTimePatchRegistry();
        _registryBuilder = new CompileTimePreludeRegistryBuilder(_registry);
        _scanner = new CompileTimePreludeAttributeScanner(_registryBuilder);
    }

    public void PatchAdd(
        MethodDefinition originalDef,
        CompileTimePreludeMethod? prefix = null,
        CompileTimePreludeMethod? postfix = null,
        CompileTimePreludeMethod? finalizer = null,
        CompileTimePreludeMethod? transpiler = null)
        => _registryBuilder.PatchAdd(originalDef, prefix, postfix, finalizer, transpiler);

    public void PatchAdd(MethodReference originalDef, CompileTimePreludePatch patch)
        => _registryBuilder.PatchAdd(originalDef, patch);

    public void PatchAdd(MethodReference originalDef, HarmonyPatchType patchType, CompileTimePreludeMethod patchMethod)
        => _registryBuilder.PatchAdd(originalDef, patchType, patchMethod);

    public void PatchAddPrefix(MethodReference originalDef, CompileTimePreludeMethod prefix)
        => _registryBuilder.PatchAddPrefix(originalDef, prefix);

    public void PatchAddPostfix(MethodReference originalDef, CompileTimePreludeMethod prefix)
        => _registryBuilder.PatchAddPostfix(originalDef, prefix);

    public void PatchAddFinalizer(MethodReference originalDef, CompileTimePreludeMethod prefix)
        => _registryBuilder.PatchAddFinalizer(originalDef, prefix);

    public void ScanAndPatchAll(AssemblyDefinition patchAssemblyDef)
        => _scanner.ScanAndPatchAll(patchAssemblyDef);

    public void ScanAndPatchCategory(AssemblyDefinition patchAssemblyDef, string category)
        => _scanner.ScanAndPatchCategory(patchAssemblyDef, category);

    public void ScanAndPatchUncategorized(AssemblyDefinition patchAssemblyDef)
        => _scanner.ScanAndPatchUncategorized(patchAssemblyDef);

    public void ScanAndPatch(TypeReference containerTypeRef)
        => _scanner.ScanAndPatch(containerTypeRef);

    public void ScanAndPatch(TypeDefinition containerTypeDef)
        => _scanner.ScanAndPatch(containerTypeDef);
    
    public void Commit()
        => _backend.Commit(_registry);
}