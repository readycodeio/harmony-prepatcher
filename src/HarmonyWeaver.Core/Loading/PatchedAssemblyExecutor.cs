using System;
using System.IO;
using System.Reflection;

namespace HarmonyWeaver.Core.Loading
{
    /// <summary>
    /// Executes methods in patched assemblies using isolated load contexts
    /// </summary>
    public class PatchedAssemblyExecutor : IDisposable
    {
        private readonly IsolatedAssemblyLoadContext _loadContext;
        private readonly Assembly _patchedAssembly;

        public PatchedAssemblyExecutor(string patchedAssemblyPath)
        {
            if (!File.Exists(patchedAssemblyPath))
                throw new FileNotFoundException($"Patched assembly not found: {patchedAssemblyPath}");

            var assemblyDirectory = Path.GetDirectoryName(patchedAssemblyPath) ?? 
                throw new ArgumentException("Invalid assembly path", nameof(patchedAssemblyPath));

            var contextName = $"PatchedContext_{Guid.NewGuid():N}";
            _loadContext = new IsolatedAssemblyLoadContext(contextName, assemblyDirectory);
            _patchedAssembly = _loadContext.LoadAssemblyFromPath(patchedAssemblyPath);
        }

        /// <summary>
        /// Get a type from the patched assembly
        /// </summary>
        public Type? GetType(string typeName)
        {
            return _patchedAssembly.GetType(typeName);
        }

        /// <summary>
        /// Create an instance of a type from the patched assembly
        /// </summary>
        public object? CreateInstance(string typeName)
        {
            var type = GetType(typeName);
            return type != null ? Activator.CreateInstance(type) : null;
        }

        /// <summary>
        /// Create an instance of a type from the patched assembly
        /// </summary>
        public object? CreateInstance(Type type)
        {
            return Activator.CreateInstance(type);
        }

        /// <summary>
        /// Invoke a method on an instance from the patched assembly
        /// </summary>
        public object? InvokeMethod(object instance, string methodName, params object[] parameters)
        {
            var method = instance.GetType().GetMethod(methodName);
            return method?.Invoke(instance, parameters);
        }

        /// <summary>
        /// Get the loaded patched assembly
        /// </summary>
        public Assembly PatchedAssembly => _patchedAssembly;

        public void Dispose()
        {
            _loadContext?.Unload();
        }
    }
}