using Mono.Cecil;

namespace PreludeLib.CompileTime.Public;

internal interface ICompileTimePreludeAttributeScanner
{
    void ScanAndPatchAll(AssemblyDefinition patchAssemblyDef);
    void ScanAndPatchCategory(AssemblyDefinition patchAssemblyDef, string category);
    void ScanAndPatchUncategorized(AssemblyDefinition patchAssemblyDef);
    
    void ScanAndPatch(TypeReference containerTypeRef);
    void ScanAndPatch(TypeDefinition containerTypeDef);
}