using HarmonyWeaver.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace HarmonyWeaver.Core.Utils
{
    /// <summary>
    /// Reflection-based patch discovery utility for runtime callback application
    /// </summary>
    public static class ReflectionPatchDiscovery
    {
        /// <summary>
        /// Discover patch information from runtime assemblies (used during callback application)
        /// </summary>
        public static List<SimplePatchInfo> DiscoverPatches(IEnumerable<Assembly> patchAssemblies)
        {
            var patches = new Dictionary<string, SimplePatchInfo>();

            foreach (var assembly in patchAssemblies)
            {
                foreach (var type in assembly.GetTypes())
                {
                    var typePatches = DiscoverPatchesFromType(type, assembly);
                    foreach (var patch in typePatches)
                    {
                        var key = $"{patch.TargetTypeName}.{patch.TargetMethodName}";
                        
                        if (patches.TryGetValue(key, out var existingPatch))
                        {
                            MergePatchInfo(existingPatch, patch);
                        }
                        else
                        {
                            patches[key] = patch;
                        }
                    }
                }
            }

            return patches.Values.Where(p => p.HasAnyPatchMethods).ToList();
        }

        private static List<SimplePatchInfo> DiscoverPatchesFromType(Type type, Assembly assembly)
        {
            var patches = new List<SimplePatchInfo>();

            // Get type-level HarmonyPatch attributes
            var typeLevelTargets = GetTargetsFromTypeAttributes(type);

            // Process each method in the type
            foreach (var method in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (method.DeclaringType != type) continue; // Skip inherited methods

                // Get method-level HarmonyPatch attributes
                var methodLevelTargets = GetTargetsFromMethodAttributes(method);

                // Combine type-level and method-level targets
                var allTargets = new List<PatchTarget>();
                
                if (methodLevelTargets.Any())
                {
                    allTargets.AddRange(methodLevelTargets);
                    // If method targets don't specify type, inherit from type-level
                    foreach (var target in allTargets.Where(t => string.IsNullOrEmpty(t.TargetTypeName)))
                    {
                        if (typeLevelTargets.Any())
                        {
                            target.TargetTypeName = typeLevelTargets[0].TargetTypeName;
                        }
                    }
                }
                else if (typeLevelTargets.Any())
                {
                    allTargets.AddRange(typeLevelTargets);
                }

                // Create patch info for each target
                foreach (var target in allTargets)
                {
                    if (string.IsNullOrEmpty(target.TargetTypeName))
                        continue;

                    var patch = new SimplePatchInfo
                    {
                        TargetTypeName = target.TargetTypeName,
                        TargetMethodName = target.TargetMethodName,
                        PatchAssemblyPath = assembly.Location,
                        PatchTypeName = type.FullName ?? type.Name
                    };

                    // Determine what kind of patch method this is
                    if (IsPrefix(method))
                    {
                        patch.PrefixMethodName = method.Name;
                        patch.PrefixReturnsBool = method.ReturnType == typeof(bool);
                    }
                    else if (IsPostfix(method))
                    {
                        patch.PostfixMethodName = method.Name;
                    }
                    else if (IsFinalizer(method))
                    {
                        patch.FinalizerMethodName = method.Name;
                    }

                    if (patch.HasAnyPatchMethods)
                    {
                        patches.Add(patch);
                    }
                }
            }

            return patches;
        }

        private static List<PatchTarget> GetTargetsFromTypeAttributes(Type type)
        {
            var targets = new List<PatchTarget>();

            var harmonyPatchAttrs = type.GetCustomAttributes(false)
                .Where(attr => attr.GetType().Name == "HarmonyPatch");

            foreach (var attr in harmonyPatchAttrs)
            {
                var target = ParsePatchTarget(attr);
                if (target != null)
                {
                    targets.Add(target);
                }
            }

            return targets;
        }

        private static List<PatchTarget> GetTargetsFromMethodAttributes(MethodInfo method)
        {
            var targets = new List<PatchTarget>();

            var harmonyPatchAttrs = method.GetCustomAttributes(false)
                .Where(attr => attr.GetType().Name == "HarmonyPatch");

            foreach (var attr in harmonyPatchAttrs)
            {
                var target = ParsePatchTarget(attr);
                if (target != null)
                {
                    targets.Add(target);
                }
            }

            return targets;
        }

        private static PatchTarget? ParsePatchTarget(object attribute)
        {
            var attrType = attribute.GetType();
            var target = new PatchTarget();

            // Parse HarmonyPatch constructor arguments via reflection
            try
            {
                // Try to access the internal fields that store constructor arguments
                var fields = attrType.GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
                
                foreach (var field in fields)
                {
                    var value = field.GetValue(attribute);
                    
                    if (value is Type type)
                    {
                        target.TargetTypeName = type.FullName;
                    }
                    else if (value is string str && !string.IsNullOrEmpty(str))
                    {
                        target.TargetMethodName = str;
                    }
                }

                // Also try common property names
                var targetTypeProperty = attrType.GetProperty("TargetType");
                var methodNameProperty = attrType.GetProperty("MethodName");

                if (targetTypeProperty?.GetValue(attribute) is Type targetType)
                {
                    target.TargetTypeName = targetType.FullName;
                }

                if (methodNameProperty?.GetValue(attribute) is string methodName)
                {
                    target.TargetMethodName = methodName;
                }
            }
            catch
            {
                // If reflection fails, we can't parse this attribute
                return null;
            }

            return !string.IsNullOrEmpty(target.TargetTypeName) ? target : null;
        }

        private static bool IsPrefix(MethodInfo method)
        {
            return method.GetCustomAttributes(false)
                .Any(attr => attr.GetType().Name == "HarmonyPrefix") ||
                method.Name == "Prefix" || method.Name.EndsWith("Prefix");
        }

        private static bool IsPostfix(MethodInfo method)
        {
            return method.GetCustomAttributes(false)
                .Any(attr => attr.GetType().Name == "HarmonyPostfix") ||
                method.Name == "Postfix" || method.Name.EndsWith("Postfix");
        }

        private static bool IsFinalizer(MethodInfo method)
        {
            return method.GetCustomAttributes(false)
                .Any(attr => attr.GetType().Name == "HarmonyFinalizer") ||
                method.Name == "Finalizer" || method.Name.EndsWith("Finalizer");
        }

        private static void MergePatchInfo(SimplePatchInfo existing, SimplePatchInfo newPatch)
        {
            if (!string.IsNullOrEmpty(newPatch.PrefixMethodName))
            {
                existing.PrefixMethodName = newPatch.PrefixMethodName;
                existing.PrefixReturnsBool = newPatch.PrefixReturnsBool;
            }

            if (!string.IsNullOrEmpty(newPatch.PostfixMethodName))
            {
                existing.PostfixMethodName = newPatch.PostfixMethodName;
            }

            if (!string.IsNullOrEmpty(newPatch.FinalizerMethodName))
            {
                existing.FinalizerMethodName = newPatch.FinalizerMethodName;
            }
        }
    }
}