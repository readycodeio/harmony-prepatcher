using System.Reflection;

namespace PreludeLib.Utils
{
    public interface IRuntimeAssemblyLoader
    {
        Assembly LoadAssemblyFrom(string assemblyPath);
    }
}