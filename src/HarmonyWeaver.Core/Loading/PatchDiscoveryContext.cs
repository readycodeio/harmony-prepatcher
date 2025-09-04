using HarmonyWeaver.Core.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

namespace HarmonyWeaver.Core.Loading
{
    /// <summary>
    /// Isolated context for discovering patches without polluting the main AppDomain
    /// </summary>
    public class PatchDiscoveryContext : AssemblyLoadContext, IDisposable
    {
        private readonly List<Assembly> _loadedAssemblies = new List<Assembly>();

        public PatchDiscoveryContext() : base("PatchDiscovery", isCollectible: true)
        {
        }

        /// <summary>
        /// Load an assembly for patch discovery
        /// </summary>
        public Assembly LoadAssemblyForDiscovery(string assemblyPath)
        {
            if (!File.Exists(assemblyPath))
                throw new FileNotFoundException($"Assembly not found: {assemblyPath}");

            var assembly = LoadFromAssemblyPath(assemblyPath);
            _loadedAssemblies.Add(assembly);
            return assembly;
        }

        /// <summary>
        /// Discover patch information from loaded assemblies without using Mono.Cecil
        /// This uses reflection to analyze the patches, then we'll use the information
        /// to guide the Cecil-based patching process
        /// </summary>
        public IEnumerable<SimplePatchInfo> DiscoverPatches()
        {
            var patches = new List<SimplePatchInfo>();

            foreach (var assembly in _loadedAssemblies)
            {
                foreach (var type in assembly.GetTypes())
                {
                    patches.AddRange(DiscoverPatchesFromType(type));
                }
            }

            return patches;
        }

        private IEnumerable<SimplePatchInfo> DiscoverPatchesFromType(Type type)
        {
            var patches = new Dictionary<string, SimplePatchInfo>(); // Key: "TypeName.MethodName"

            // Get type-level HarmonyPatch attributes
            var typeLevelPatches = GetHarmonyPatchAttributesFromType(type);

            // Process each method in the type
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
            {
                var methodLevelPatches = GetHarmonyPatchAttributesFromMethod(method);
                
                // Combine type-level and method-level patch information
                var combinedPatches = new List<SimplePatchInfo>();
                
                if (methodLevelPatches.Any())
                {
                    combinedPatches.AddRange(methodLevelPatches);
                    // Inherit target type from type-level if not specified
                    foreach (var patch in combinedPatches.Where(p => string.IsNullOrEmpty(p.TargetTypeName)))
                    {
                        if (typeLevelPatches.Any())
                        {
                            patch.TargetTypeName = typeLevelPatches[0].TargetTypeName;
                        }
                    }
                }
                else if (typeLevelPatches.Any())
                {
                    combinedPatches.AddRange(typeLevelPatches);
                }

                // Process each patch and add method information
                foreach (var patch in combinedPatches)
                {
                    if (string.IsNullOrEmpty(patch.TargetTypeName))
                        continue;

                    var key = $"{patch.TargetTypeName}.{patch.TargetMethodName ?? ""}";
                    
                    if (!patches.TryGetValue(key, out var existingPatch))
                    {
                        existingPatch = new SimplePatchInfo
                        {
                            TargetTypeName = patch.TargetTypeName,
                            TargetMethodName = patch.TargetMethodName,
                            PatchAssemblyPath = assembly.Location,
                            PatchTypeName = type.FullName ?? type.Name
                        };
                        patches[key] = existingPatch;
                    }

                    // Determine what kind of patch method this is
                    if (IsPrefix(method))
                    {
                        existingPatch.PrefixMethodName = method.Name;
                        existingPatch.PrefixReturnsBool = method.ReturnType == typeof(bool);
                        existingPatch.PrefixParameters = method.GetParameters().Select(p => new SimplePatchParameterInfo
                        {
                            Name = p.Name ?? "",
                            TypeName = p.ParameterType.FullName ?? p.ParameterType.Name,
                            IsByReference = p.ParameterType.IsByRef
                        }).ToList();
                    }
                    else if (IsPostfix(method))
                    {
                        existingPatch.PostfixMethodName = method.Name;
                        existingPatch.PostfixParameters = method.GetParameters().Select(p => new SimplePatchParameterInfo
                        {
                            Name = p.Name ?? "",
                            TypeName = p.ParameterType.FullName ?? p.ParameterType.Name,
                            IsByReference = p.ParameterType.IsByRef
                        }).ToList();
                    }
                    else if (IsFinalizer(method))
                    {
                        existingPatch.FinalizerMethodName = method.Name;
                        existingPatch.FinalizerParameters = method.GetParameters().Select(p => new SimplePatchParameterInfo
                        {
                            Name = p.Name ?? "",
                            TypeName = p.ParameterType.FullName ?? p.ParameterType.Name,
                            IsByReference = p.ParameterType.IsByRef
                        }).ToList();
                    }
                }
            }

            return patches.Values.Where(p => 
                !string.IsNullOrEmpty(p.PrefixMethodName) || 
                !string.IsNullOrEmpty(p.PostfixMethodName) || 
                !string.IsNullOrEmpty(p.FinalizerMethodName));
        }

        private List<SimplePatchInfo> GetHarmonyPatchAttributesFromType(Type type)
        {
            var patches = new List<SimplePatchInfo>();
            
            var harmonyPatchAttrs = type.GetCustomAttributes(false)
                .Where(attr => attr.GetType().Name == "HarmonyPatchAttribute" || attr.GetType().Name == "HarmonyPatch");

            foreach (var attr in harmonyPatchAttrs)
            {
                var patch = ParseHarmonyPatchFromReflection(attr);
                if (patch != null)
                {
                    patches.Add(patch);
                }
            }

            return patches;
        }

        private List<SimplePatchInfo> GetHarmonyPatchAttributesFromMethod(MethodInfo method)
        {
            var patches = new List<SimplePatchInfo>();
            
            var harmonyPatchAttrs = method.GetCustomAttributes(false)
                .Where(attr => attr.GetType().Name == "HarmonyPatchAttribute" || attr.GetType().Name == "HarmonyPatch");

            foreach (var attr in harmonyPatchAttrs)
            {
                var patch = ParseHarmonyPatchFromReflection(attr);
                if (patch != null)
                {
                    patches.Add(patch);
                }
            }

            return patches;
        }

        private SimplePatchInfo? ParseHarmonyPatchFromReflection(object attribute)
        {
            var attrType = attribute.GetType();
            
            // Get constructor arguments via reflection
            // This is a simplified approach - in a real implementation we might need more robust parsing
            var targetTypeProperty = attrType.GetProperty("TargetType");
            var methodNameProperty = attrType.GetProperty("MethodName");
            
            // Try to get target type from constructor or properties
            Type? targetType = null;
            string? methodName = null;

            if (targetTypeProperty != null)
            {
                targetType = targetTypeProperty.GetValue(attribute) as Type;
            }

            if (methodNameProperty != null)
            {
                methodName = methodNameProperty.GetValue(attribute) as string;
            }

            // For attributes with constructor arguments, we need to parse them
            // This is a simplified version - real Harmony parsing is more complex
            if (targetType != null)
            {
                return new SimplePatchInfo
                {
                    TargetTypeName = targetType.FullName ?? targetType.Name,
                    TargetMethodName = methodName
                };
            }

            return null;
        }

        private bool IsPrefix(MethodInfo method)
        {
            return method.GetCustomAttributes(false)
                .Any(attr => attr.GetType().Name == "HarmonyPrefixAttribute" || attr.GetType().Name == "HarmonyPrefix") ||
                method.Name == "Prefix" || method.Name.EndsWith("Prefix");
        }

        private bool IsPostfix(MethodInfo method)
        {
            return method.GetCustomAttributes(false)
                .Any(attr => attr.GetType().Name == "HarmonyPostfixAttribute" || attr.GetType().Name == "HarmonyPostfix") ||
                method.Name == "Postfix" || method.Name.EndsWith("Postfix");
        }

        private bool IsFinalizer(MethodInfo method)
        {
            return method.GetCustomAttributes(false)
                .Any(attr => attr.GetType().Name == "HarmonyFinalizerAttribute" || attr.GetType().Name == "HarmonyFinalizer") ||
                method.Name == "Finalizer" || method.Name.EndsWith("Finalizer");
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            // Let the default context handle dependency resolution
            return null;
        }

        public new void Dispose()
        {
            Unload();
        }
    }
}