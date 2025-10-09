extern alias OfficialCecil;
using OfficialCecil::Mono.Cecil;
using PreludeLib.CompileTime.Registry;

namespace PreludeLib.CompileTime.Backend;

public interface ICompileTimeBackend
{
    void Commit(ICompileTimePatchRegistry registry);
    
    IEnumerable<AssemblyDefinition> PatchedAssemblies { get; }
}