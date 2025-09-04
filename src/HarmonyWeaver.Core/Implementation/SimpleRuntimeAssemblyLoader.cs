using HarmonyWeaver.Core.Interfaces;
using System;
using System.IO;
using System.Reflection;

namespace HarmonyWeaver.Core.Implementation
{
    /// <summary>
    /// Simple assembly loader that loads assemblies immediately without retry logic
    /// </summary>
    public class SimpleRuntimeAssemblyLoader : IRuntimeAssemblyLoader
    {
        public Assembly LoadAssembly(string assemblyPath)
        {
            if (string.IsNullOrWhiteSpace(assemblyPath))
                throw new ArgumentNullException(nameof(assemblyPath));

            if (!File.Exists(assemblyPath))
                throw new FileNotFoundException($"Assembly file not found: {assemblyPath}");

            try
            {
                return Assembly.LoadFrom(assemblyPath);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to load assembly from {assemblyPath}: {ex.Message}", ex);
            }
        }
    }
}