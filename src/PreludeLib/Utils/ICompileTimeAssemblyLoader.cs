extern alias OfficialCecil;
using OfficialCecil::Mono.Cecil;

namespace PreludeLib.Utils;

public interface ICompileTimeAssemblyLoader
{
    AssemblyDefinition LoadAssemblyFrom(string assemblyPath, ReaderParameters readerParameters);
}
