using Mono.Cecil;
using System.Collections.Generic;

namespace HarmonyWeaver.Core.Interfaces
{
    /// <summary>
    /// Interface for loading Mono.Cecil assemblies with explicit control over loading options
    /// </summary>
    public interface ICecilAssemblyLoader
    {
        /// <summary>
        /// Load an assembly with explicit read-write and retry options
        /// </summary>
        /// <param name="assemblyPath">Path to the assembly file</param>
        /// <param name="readWrite">Whether to open for read-write (true) or read-only (false)</param>
        /// <param name="maxRetries">Maximum number of retry attempts for file locking issues</param>
        /// <returns>The loaded assembly definition</returns>
        AssemblyDefinition LoadAssembly(string assemblyPath, bool readWrite = true, int maxRetries = 5);

        /// <summary>
        /// Load multiple assemblies with the same options
        /// </summary>
        /// <param name="assemblyPaths">Paths to the assembly files</param>
        /// <param name="readWrite">Whether to open for read-write (true) or read-only (false)</param>
        /// <param name="maxRetries">Maximum number of retry attempts for file locking issues</param>
        /// <returns>Collection of loaded assembly definitions</returns>
        IEnumerable<AssemblyDefinition> LoadAssemblies(IEnumerable<string> assemblyPaths, bool readWrite = true, int maxRetries = 5);

        /// <summary>
        /// Load an assembly for patch scanning (read-only, optimized for Windows compatibility)
        /// </summary>
        /// <param name="assemblyPath">Path to the patch assembly file</param>
        /// <param name="maxRetries">Maximum number of retry attempts</param>
        /// <returns>The loaded assembly definition</returns>
        AssemblyDefinition LoadAssemblyForScanning(string assemblyPath, int maxRetries = 10);

        /// <summary>
        /// Load an assembly for patching (read-write, required for IL modification)
        /// </summary>
        /// <param name="assemblyPath">Path to the target assembly file</param>
        /// <param name="maxRetries">Maximum number of retry attempts</param>
        /// <returns>The loaded assembly definition</returns>
        AssemblyDefinition LoadAssemblyForPatching(string assemblyPath, int maxRetries = 10);

        /// <summary>
        /// Dispose and release resources for loaded assemblies
        /// </summary>
        void Dispose();
    }
}