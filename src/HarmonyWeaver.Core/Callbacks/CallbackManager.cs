using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace HarmonyWeaver.Core.Callbacks
{
    /// <summary>
    /// Manages callback registration and lifecycle for patched methods
    /// Uses reflection to set/clear callbacks and tracks patch state
    /// </summary>
    public class CallbackManager
    {
        private readonly ConcurrentDictionary<string, MethodCallbacks> _registeredCallbacks = new();
        private readonly ConcurrentDictionary<string, bool> _appliedPatches = new();

        /// <summary>
        /// Register callbacks for a specific method
        /// </summary>
        /// <param name="typeName">Full name of the type containing the method</param>
        /// <param name="methodName">Name of the method</param>
        /// <param name="callbacks">Callbacks to register</param>
        /// <returns>Unique callback key for this method</returns>
        public string RegisterCallbacks(string typeName, string methodName, MethodCallbacks callbacks)
        {
            if (string.IsNullOrWhiteSpace(typeName))
                throw new ArgumentNullException(nameof(typeName));
            if (string.IsNullOrWhiteSpace(methodName))
                throw new ArgumentNullException(nameof(methodName));
            if (callbacks == null)
                throw new ArgumentNullException(nameof(callbacks));

            var key = CreateMethodKey(typeName, methodName);
            
            if (_registeredCallbacks.ContainsKey(key))
                throw new InvalidOperationException($"Callbacks already registered for {typeName}.{methodName}");

            _registeredCallbacks[key] = callbacks;
            return key;
        }

        /// <summary>
        /// Apply callbacks to a loaded assembly using reflection
        /// </summary>
        /// <param name="assembly">Assembly containing the patched methods</param>
        /// <returns>Number of patches successfully applied</returns>
        public int ApplyCallbacks(Assembly assembly)
        {
            if (assembly == null)
                throw new ArgumentNullException(nameof(assembly));

            int appliedCount = 0;

            foreach (var kvp in _registeredCallbacks)
            {
                var key = kvp.Key;
                var callbacks = kvp.Value;

                if (_appliedPatches.GetValueOrDefault(key, false))
                    continue; // Already applied

                try
                {
                    if (ApplyCallbacksToMethod(assembly, key, callbacks))
                    {
                        _appliedPatches[key] = true;
                        appliedCount++;
                    }
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Failed to apply callbacks for {key}: {ex.Message}", ex);
                }
            }

            return appliedCount;
        }

        /// <summary>
        /// Clear callbacks for a specific method
        /// </summary>
        /// <param name="typeName">Full name of the type containing the method</param>
        /// <param name="methodName">Name of the method</param>
        public void ClearCallbacks(string typeName, string methodName)
        {
            var key = CreateMethodKey(typeName, methodName);
            
            if (_registeredCallbacks.TryGetValue(key, out var callbacks))
            {
                callbacks.Clear();
                _registeredCallbacks.TryRemove(key, out _);
                _appliedPatches.TryRemove(key, out _);
            }
        }

        /// <summary>
        /// Clear all registered callbacks
        /// </summary>
        public void ClearAllCallbacks()
        {
            foreach (var callbacks in _registeredCallbacks.Values)
            {
                callbacks.Clear();
            }
            
            _registeredCallbacks.Clear();
            _appliedPatches.Clear();
        }

        /// <summary>
        /// Get information about applied patches
        /// </summary>
        /// <returns>Dictionary of method keys and their application status</returns>
        public IReadOnlyDictionary<string, bool> GetPatchStatus()
        {
            return _appliedPatches.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        /// <summary>
        /// Check if a specific method has been patched
        /// </summary>
        /// <param name="typeName">Full name of the type containing the method</param>
        /// <param name="methodName">Name of the method</param>
        /// <returns>True if the method has been patched</returns>
        public bool IsPatched(string typeName, string methodName)
        {
            var key = CreateMethodKey(typeName, methodName);
            return _appliedPatches.GetValueOrDefault(key, false);
        }

        private string CreateMethodKey(string typeName, string methodName)
        {
            return $"{typeName}.{methodName}";
        }

        private bool ApplyCallbacksToMethod(Assembly assembly, string methodKey, MethodCallbacks callbacks)
        {
            // Parse the method key to get type and method names
            var parts = methodKey.Split('.');
            if (parts.Length < 2)
                return false;

            var methodName = parts[^1];
            var typeName = string.Join(".", parts[..^1]);

            // Find the type in the assembly
            var type = assembly.GetType(typeName);
            if (type == null)
                return false;

            // Find callback fields that should have been injected by the IL weaver
            var prefixField = type.GetField($"__harmony_prefix_{methodName}", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            var postfixField = type.GetField($"__harmony_postfix_{methodName}", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            var finalizerField = type.GetField($"__harmony_finalizer_{methodName}", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

            bool appliedAny = false;

            // Set the callback fields using reflection
            if (prefixField != null && callbacks.Prefix != null)
            {
                prefixField.SetValue(null, callbacks.Prefix);
                appliedAny = true;
            }

            if (postfixField != null && callbacks.Postfix != null)
            {
                postfixField.SetValue(null, callbacks.Postfix);
                appliedAny = true;
            }

            if (finalizerField != null && callbacks.Finalizer != null)
            {
                finalizerField.SetValue(null, callbacks.Finalizer);
                appliedAny = true;
            }

            return appliedAny;
        }
    }
}