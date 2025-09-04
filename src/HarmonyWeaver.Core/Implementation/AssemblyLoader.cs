using HarmonyWeaver.Core.Interfaces;
using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HarmonyWeaver.Core.Implementation
{
    /// <summary>
    /// Default implementation of IAssemblyLoader using Mono.Cecil
    /// </summary>
    public class AssemblyLoader : IAssemblyLoader
    {
        private readonly List<AssemblyDefinition> _loadedAssemblies = new List<AssemblyDefinition>();
        private readonly ReaderParameters _readerParameters;

        public AssemblyLoader()
        {
            _readerParameters = new ReaderParameters
            {
                ReadWrite = true,
                InMemory = true
            };
        }

        public AssemblyDefinition LoadAssembly(string assemblyPath)
        {
            if (string.IsNullOrWhiteSpace(assemblyPath))
                throw new ArgumentNullException(nameof(assemblyPath));

            if (!File.Exists(assemblyPath))
                throw new FileNotFoundException($"Assembly file not found: {assemblyPath}");

            try
            {
                var assembly = AssemblyDefinition.ReadAssembly(assemblyPath, _readerParameters);
                _loadedAssemblies.Add(assembly);
                return assembly;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to load assembly from {assemblyPath}: {ex.Message}", ex);
            }
        }

        public IEnumerable<AssemblyDefinition> LoadAssemblies(IEnumerable<string> assemblyPaths)
        {
            if (assemblyPaths == null)
                throw new ArgumentNullException(nameof(assemblyPaths));

            var paths = assemblyPaths.ToList();
            var assemblies = new List<AssemblyDefinition>();

            foreach (var path in paths)
            {
                assemblies.Add(LoadAssembly(path));
            }

            return assemblies;
        }

        public void Dispose()
        {
            foreach (var assembly in _loadedAssemblies)
            {
                assembly?.Dispose();
            }
            _loadedAssemblies.Clear();
        }
    }
}