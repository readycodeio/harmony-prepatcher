using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace HarmonyWeaver.Core.Loading
{
    /// <summary>
    /// Isolated assembly load context for loading patched assemblies without conflicts
    /// </summary>
    public class IsolatedAssemblyLoadContext : AssemblyLoadContext
    {
        private readonly string _assemblyDirectory;

        public IsolatedAssemblyLoadContext(string name, string assemblyDirectory) : base(name, isCollectible: true)
        {
            _assemblyDirectory = assemblyDirectory ?? throw new ArgumentNullException(nameof(assemblyDirectory));
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            // Try to load from our directory first
            var assemblyPath = Path.Combine(_assemblyDirectory, assemblyName.Name + ".dll");
            
            if (File.Exists(assemblyPath))
            {
                return LoadFromAssemblyPath(assemblyPath);
            }

            // If not found in our directory, let the default context handle it
            return null;
        }

        /// <summary>
        /// Load an assembly from a specific path within this context
        /// </summary>
        public Assembly LoadAssemblyFromPath(string assemblyPath)
        {
            if (!File.Exists(assemblyPath))
                throw new FileNotFoundException($"Assembly not found: {assemblyPath}");

            return LoadFromAssemblyPath(assemblyPath);
        }
    }
}