using System.Reflection;

namespace HarmonyWeaver.Core.Interfaces
{
    /// <summary>
    /// Interface for loading .NET runtime assemblies (for executing patched code)
    /// </summary>
    public interface IRuntimeAssemblyLoader
    {
        /// <summary>
        /// Load a .NET runtime assembly from a file path
        /// </summary>
        /// <param name="assemblyPath">Path to the assembly file</param>
        /// <returns>The loaded runtime assembly</returns>
        Assembly LoadAssembly(string assemblyPath);
    }
}