using HarmonyWeaver.Core.Models;
using Mono.Cecil;
using System.Collections.Generic;
using System.Linq;

namespace HarmonyWeaver.Core.Utils
{
    /// <summary>
    /// Cecil-based patch discovery utility for the assembly patching phase
    /// </summary>
    public static class CecilPatchDiscovery
    {
        /// <summary>
        /// Discover patch information from Cecil assemblies (used during patching phase)
        /// </summary>
        public static List<SimplePatchInfo> DiscoverPatches(IEnumerable<AssemblyDefinition> patchAssemblies)
        {
            var patches = new Dictionary<string, SimplePatchInfo>();

            foreach (var assembly in patchAssemblies)
            {
                foreach (var type in assembly.MainModule.Types)
                {
                    if (type.Name == "<Module>") continue;

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

        private static List<SimplePatchInfo> DiscoverPatchesFromType(TypeDefinition type, AssemblyDefinition assembly)
        {
            var patches = new List<SimplePatchInfo>();

            // Get type-level HarmonyPatch attributes
            var typeLevelTargets = GetTargetsFromTypeAttributes(type);

            // Process each method in the type
            foreach (var method in type.Methods)
            {
                if (method.IsConstructor || method.Name.StartsWith("get_") || method.Name.StartsWith("set_"))
                    continue;

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
                        PatchAssemblyPath = assembly.MainModule.FileName,
                        PatchTypeName = type.FullName
                    };

                    // Determine what kind of patch method this is
                    if (IsPrefix(method))
                    {
                        patch.PrefixMethodName = method.Name;
                        patch.PrefixReturnsBool = method.ReturnType.Name == "Boolean";
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

        private static List<PatchTarget> GetTargetsFromTypeAttributes(TypeDefinition type)
        {
            var targets = new List<PatchTarget>();

            foreach (var attr in type.CustomAttributes)
            {
                if (attr.AttributeType.Name == "HarmonyPatch")
                {
                    var target = ParsePatchTarget(attr);
                    if (target != null)
                    {
                        targets.Add(target);
                    }
                }
            }

            return targets;
        }

        private static List<PatchTarget> GetTargetsFromMethodAttributes(MethodDefinition method)
        {
            var targets = new List<PatchTarget>();

            foreach (var attr in method.CustomAttributes)
            {
                if (attr.AttributeType.Name == "HarmonyPatch")
                {
                    var target = ParsePatchTarget(attr);
                    if (target != null)
                    {
                        targets.Add(target);
                    }
                }
            }

            return targets;
        }

        private static PatchTarget? ParsePatchTarget(CustomAttribute attr)
        {
            var target = new PatchTarget();

            // Parse constructor arguments
            if (attr.HasConstructorArguments)
            {
                foreach (var arg in attr.ConstructorArguments)
                {
                    if (arg.Type.Name == "Type")
                    {
                        // Target type specified directly
                        if (arg.Value is TypeReference typeRef)
                        {
                            target.TargetTypeName = typeRef.FullName;
                        }
                    }
                    else if (arg.Type.Name == "String")
                    {
                        // Could be method name
                        var stringValue = arg.Value?.ToString();
                        if (!string.IsNullOrEmpty(stringValue))
                        {
                            target.TargetMethodName = stringValue;
                        }
                    }
                }
            }

            return !string.IsNullOrEmpty(target.TargetTypeName) ? target : null;
        }

        private static bool IsPrefix(MethodDefinition method)
        {
            foreach (var attr in method.CustomAttributes)
            {
                if (attr.AttributeType.Name == "HarmonyPrefix")
                {
                    return true;
                }
            }
            return method.Name == "Prefix" || method.Name.EndsWith("Prefix");
        }

        private static bool IsPostfix(MethodDefinition method)
        {
            foreach (var attr in method.CustomAttributes)
            {
                if (attr.AttributeType.Name == "HarmonyPostfix")
                {
                    return true;
                }
            }
            return method.Name == "Postfix" || method.Name.EndsWith("Postfix");
        }

        private static bool IsFinalizer(MethodDefinition method)
        {
            foreach (var attr in method.CustomAttributes)
            {
                if (attr.AttributeType.Name == "HarmonyFinalizer")
                {
                    return true;
                }
            }
            return method.Name == "Finalizer" || method.Name.EndsWith("Finalizer");
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

    /// <summary>
    /// Represents a patch target (either static or dynamic)
    /// </summary>
    internal class PatchTarget
    {
        public string? TargetTypeName { get; set; }
        public string? TargetMethodName { get; set; }
    }
}