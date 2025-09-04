using Mono.Cecil;
using System.Collections.Generic;

namespace HarmonyWeaver.Core.Interfaces
{
    /// <summary>
    /// Interface for loading assemblies for patch processing
    /// </summary>
    public interface IAssemblyLoader
    {
        /// <summary>
        /// Load an assembly from a file path
        /// </summary>
        /// <param name="assemblyPath">Path to the assembly file</param>
        /// <returns>The loaded assembly definition</returns>
        AssemblyDefinition LoadAssembly(string assemblyPath);

        /// <summary>
        /// Load multiple assemblies from file paths
        /// </summary>
        /// <param name="assemblyPaths">Paths to the assembly files</param>
        /// <returns>Collection of loaded assembly definitions</returns>
        IEnumerable<AssemblyDefinition> LoadAssemblies(IEnumerable<string> assemblyPaths);

        /// <summary>
        /// Dispose and release resources for loaded assemblies
        /// </summary>
        void Dispose();
    }
}