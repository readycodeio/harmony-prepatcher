using HarmonyWeaver.Core.Callbacks;
using HarmonyWeaver.Core.Interfaces;
using HarmonyWeaver.Core.Models;
using HarmonyWeaver.Core.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace HarmonyWeaver.Core
{
    /// <summary>
    /// Callback-based HarmonyWeaver that eliminates dependency cycles
    /// Uses callback injection instead of direct method calls
    /// </summary>
    public class CallbackHarmonyWeaver : IHarmonyWeaver, IDisposable
    {
        private readonly ICecilAssemblyLoader _cecilAssemblyLoader;
        private readonly IPatchScanner _patchScanner;
        private readonly IILWeaver _ilWeaver;
        private readonly IAssemblySaver _assemblySaver;
        private readonly IRuntimeAssemblyLoader _runtimeAssemblyLoader;
        private readonly CallbackManager _callbackManager;

        public CallbackHarmonyWeaver(
            ICecilAssemblyLoader cecilAssemblyLoader,
            IPatchScanner patchScanner,
            IILWeaver ilWeaver,
            IAssemblySaver assemblySaver,
            IRuntimeAssemblyLoader runtimeAssemblyLoader)
        {
            _cecilAssemblyLoader = cecilAssemblyLoader ?? throw new ArgumentNullException(nameof(cecilAssemblyLoader));
            _patchScanner = patchScanner ?? throw new ArgumentNullException(nameof(patchScanner));
            _ilWeaver = ilWeaver ?? throw new ArgumentNullException(nameof(ilWeaver));
            _assemblySaver = assemblySaver ?? throw new ArgumentNullException(nameof(assemblySaver));
            _runtimeAssemblyLoader = runtimeAssemblyLoader ?? throw new ArgumentNullException(nameof(runtimeAssemblyLoader));
            _callbackManager = new CallbackManager();
        }

        public IEnumerable<string> ProcessPatches(
            IEnumerable<string> patchAssemblyPaths,
            IEnumerable<string> targetAssemblyPaths,
            string outputDirectory)
        {
            var outputPaths = targetAssemblyPaths.Select(path => 
            {
                var fileName = Path.GetFileNameWithoutExtension(path);
                var extension = Path.GetExtension(path);
                return Path.Combine(outputDirectory, $"{fileName}_patched{extension}");
            });

            return ProcessPatches(patchAssemblyPaths, targetAssemblyPaths, outputPaths);
        }

        public IEnumerable<string> ProcessPatches(
            IEnumerable<string> patchAssemblyPaths,
            IEnumerable<string> targetAssemblyPaths,
            IEnumerable<string> outputPaths)
        {
            if (patchAssemblyPaths == null) throw new ArgumentNullException(nameof(patchAssemblyPaths));
            if (targetAssemblyPaths == null) throw new ArgumentNullException(nameof(targetAssemblyPaths));
            if (outputPaths == null) throw new ArgumentNullException(nameof(outputPaths));

            var patchPaths = patchAssemblyPaths.ToList();
            var targetPaths = targetAssemblyPaths.ToList();
            var outPaths = outputPaths.ToList();

            if (targetPaths.Count != outPaths.Count)
                throw new ArgumentException("Number of target assemblies must match number of output paths");

            var result = new List<string>();

            try
            {
                // Step 1: Load patch assemblies (read-only for scanning)
                var patchAssemblies = _cecilAssemblyLoader.LoadAssemblies(patchPaths, readWrite: false, maxRetries: 10).ToList();
                
                // Step 2: Discover patches
                var allPatches = new List<PatchInfo>();
                foreach (var patchAssembly in patchAssemblies)
                {
                    allPatches.AddRange(_patchScanner.ScanForPatches(patchAssembly));
                }
                
                if (!allPatches.Any())
                {
                    throw new InvalidOperationException("No HarmonyPatch attributes found in the provided patch assemblies");
                }

                // Step 3: Process each target assembly
                for (int i = 0; i < targetPaths.Count; i++)
                {
                    var targetPath = targetPaths[i];
                    var outputPath = outPaths[i];

                    // Load target assembly (read-write for patching)
                    var targetAssembly = _cecilAssemblyLoader.LoadAssemblyForPatching(targetPath, maxRetries: 10);
                    
                    // Resolve patches for this target assembly
                    var resolvedPatches = _patchScanner.ResolveTargets(allPatches, new[] { targetAssembly }).ToList();
                    
                    if (resolvedPatches.Any())
                    {
                        // Apply callback-based patches (no dependency cycles!)
                        _ilWeaver.ApplyPatches(targetAssembly, resolvedPatches);
                        
                        // Save the patched assembly
                        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
                        _assemblySaver.SaveAssembly(targetAssembly, outputPath);
                        
                        result.Add(outputPath);
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error processing patches: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Apply callbacks to a loaded assembly (call this after loading patched assemblies)
        /// </summary>
        /// <param name="patchedAssemblyPath">Path to the patched assembly</param>
        /// <param name="patchAssemblyPaths">Paths to assemblies containing the actual patch methods</param>
        /// <returns>Number of callbacks successfully applied</returns>
        public int ApplyCallbacks(string patchedAssemblyPath, IEnumerable<string> patchAssemblyPaths)
        {
            // Load the patched assembly for execution
            var patchedAssembly = _runtimeAssemblyLoader.LoadAssembly(patchedAssemblyPath);
            
            // Use REFLECTION-BASED discovery at runtime (cleaner separation)
            var runtimePatchAssemblies = patchAssemblyPaths.Select(path => Assembly.LoadFrom(path)).ToList();
            var discoveredPatches = ReflectionPatchDiscovery.DiscoverPatches(runtimePatchAssemblies);
            
            // Register callbacks from the discovered patches
            RegisterCallbacksFromReflectionPatches(discoveredPatches, runtimePatchAssemblies);
            
            // Apply the callbacks to the patched assembly
            return _callbackManager.ApplyCallbacks(patchedAssembly);
        }

        /// <summary>
        /// Get the callback manager for direct access
        /// </summary>
        public CallbackManager CallbackManager => _callbackManager;

        private void RegisterCallbacksFromReflectionPatches(List<SimplePatchInfo> patches, List<Assembly> runtimePatchAssemblies)
        {
            foreach (var patch in patches)
            {
                if (string.IsNullOrEmpty(patch.TargetTypeName) || string.IsNullOrEmpty(patch.TargetMethodName))
                    continue;

                // Create callbacks for this patch
                var callbacks = new MethodCallbacks();

                // Find the runtime methods
                if (!string.IsNullOrEmpty(patch.PrefixMethodName))
                {
                    var runtimeMethod = FindRuntimeMethod(runtimePatchAssemblies, patch.PatchTypeName, patch.PrefixMethodName);
                    if (runtimeMethod != null)
                    {
                        callbacks.Prefix = CreatePrefixCallback(runtimeMethod, runtimeMethod.DeclaringType!);
                    }
                }

                if (!string.IsNullOrEmpty(patch.PostfixMethodName))
                {
                    var runtimeMethod = FindRuntimeMethod(runtimePatchAssemblies, patch.PatchTypeName, patch.PostfixMethodName);
                    if (runtimeMethod != null)
                    {
                        callbacks.Postfix = CreatePostfixCallback(runtimeMethod, runtimeMethod.DeclaringType!);
                    }
                }

                if (!string.IsNullOrEmpty(patch.FinalizerMethodName))
                {
                    var runtimeMethod = FindRuntimeMethod(runtimePatchAssemblies, patch.PatchTypeName, patch.FinalizerMethodName);
                    if (runtimeMethod != null)
                    {
                        callbacks.Finalizer = CreateFinalizerCallback(runtimeMethod, runtimeMethod.DeclaringType!);
                    }
                }

                // Register the callbacks if any were created
                if (callbacks.HasAnyCallbacks)
                {
                    _callbackManager.RegisterCallbacks(patch.TargetTypeName, patch.TargetMethodName, callbacks);
                }
            }
        }

        private MethodInfo? FindRuntimeMethod(List<Assembly> assemblies, string typeName, string methodName)
        {
            foreach (var assembly in assemblies)
            {
                var type = assembly.GetType(typeName);
                if (type != null)
                {
                    var method = type.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    if (method != null)
                    {
                        return method;
                    }
                }
            }

            return null;
        }


        private PrefixCallback CreatePrefixCallback(MethodInfo method, Type declaringType)
        {
            return (object[] args, out object result) =>
            {
                // Convert args to the types expected by the patch method
                var parameters = method.GetParameters();
                var convertedArgs = new object[parameters.Length];
                
                for (int i = 0; i < parameters.Length && i < args.Length; i++)
                {
                    convertedArgs[i] = ConvertArgument(args[i], parameters[i].ParameterType);
                }

                // Call the patch method
                var methodResult = method.Invoke(null, convertedArgs);
                
                // Handle ref __result parameter if present
                result = null!;
                for (int i = 0; i < parameters.Length; i++)
                {
                    if (parameters[i].Name == "__result" && parameters[i].ParameterType.IsByRef)
                    {
                        result = convertedArgs[i];
                        break;
                    }
                }

                // Return the bool result (true = continue, false = skip)
                return methodResult is bool boolResult && boolResult;
            };
        }

        private PostfixCallback CreatePostfixCallback(MethodInfo method, Type declaringType)
        {
            return (object[] args, ref object result) =>
            {
                // Convert args and prepare parameters including __result
                var parameters = method.GetParameters();
                var convertedArgs = new object[parameters.Length];
                
                int argIndex = 0;
                for (int i = 0; i < parameters.Length; i++)
                {
                    if (parameters[i].Name == "__result")
                    {
                        convertedArgs[i] = result;
                    }
                    else if (argIndex < args.Length)
                    {
                        convertedArgs[i] = ConvertArgument(args[argIndex], parameters[i].ParameterType);
                        argIndex++;
                    }
                }

                // Call the patch method
                method.Invoke(null, convertedArgs);
                
                // Update result if it was modified
                for (int i = 0; i < parameters.Length; i++)
                {
                    if (parameters[i].Name == "__result")
                    {
                        result = convertedArgs[i];
                        break;
                    }
                }
            };
        }

        private FinalizerCallback CreateFinalizerCallback(MethodInfo method, Type declaringType)
        {
            return (object[] args, Exception exception) =>
            {
                // TODO: Implement finalizer callback creation
                // For now, just call the method with available parameters
                try
                {
                    var parameters = method.GetParameters();
                    var convertedArgs = new object[parameters.Length];
                    
                    int argIndex = 0;
                    for (int i = 0; i < parameters.Length; i++)
                    {
                        if (parameters[i].Name == "__exception")
                        {
                            convertedArgs[i] = exception;
                        }
                        else if (argIndex < args.Length)
                        {
                            convertedArgs[i] = ConvertArgument(args[argIndex], parameters[i].ParameterType);
                            argIndex++;
                        }
                    }

                    method.Invoke(null, convertedArgs);
                }
                catch
                {
                    // Ignore finalizer errors to avoid breaking the main functionality
                }
            };
        }

        private object ConvertArgument(object value, Type targetType)
        {
            if (value == null || targetType.IsAssignableFrom(value.GetType()))
                return value;

            try
            {
                return Convert.ChangeType(value, targetType);
            }
            catch
            {
                return value; // Return original if conversion fails
            }
        }

        private bool IsPrefix(MethodInfo method)
        {
            return method.GetCustomAttributes(false)
                .Any(attr => attr.GetType().Name.Contains("HarmonyPrefix")) ||
                method.Name == "Prefix" || method.Name.EndsWith("Prefix");
        }

        private bool IsPostfix(MethodInfo method)
        {
            return method.GetCustomAttributes(false)
                .Any(attr => attr.GetType().Name.Contains("HarmonyPostfix")) ||
                method.Name == "Postfix" || method.Name.EndsWith("Postfix");
        }

        private bool IsFinalizer(MethodInfo method)
        {
            return method.GetCustomAttributes(false)
                .Any(attr => attr.GetType().Name.Contains("HarmonyFinalizer")) ||
                method.Name == "Finalizer" || method.Name.EndsWith("Finalizer");
        }

        public void Dispose()
        {
            _cecilAssemblyLoader?.Dispose();
            _callbackManager?.ClearAllCallbacks();
        }
    }

    /// <summary>
    /// Simple patch target information
    /// </summary>
    internal class PatchTarget
    {
        public string? TypeName { get; set; }
        public string? MethodName { get; set; }
    }
}