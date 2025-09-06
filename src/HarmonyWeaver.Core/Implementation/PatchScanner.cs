using HarmonyWeaver.Core.Interfaces;
using HarmonyWeaver.Core.Models;
using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HarmonyWeaver.Core.Implementation
{
    /// <summary>
    /// Default implementation of IPatchScanner
    /// </summary>
    public class PatchScanner : IPatchScanner
    {
        public IEnumerable<PatchInfo> ScanForPatches(AssemblyDefinition assembly)
        {
            if (assembly == null)
                throw new ArgumentNullException(nameof(assembly));

            return ScanForPatches(new[] { assembly });
        }

        public IEnumerable<PatchInfo> ScanForPatches(IEnumerable<AssemblyDefinition> assemblies)
        {
            if (assemblies == null)
                throw new ArgumentNullException(nameof(assemblies));

            var patches = new List<PatchInfo>();

            foreach (var assembly in assemblies)
            {
                foreach (var type in assembly.MainModule.Types)
                {
                    patches.AddRange(ScanTypeForPatches(type, assembly));
                }
            }

            return patches;
        }

        public IEnumerable<PatchInfo> ResolveTargets(IEnumerable<PatchInfo> patches, IEnumerable<AssemblyDefinition> targetAssemblies)
        {
            if (patches == null)
                throw new ArgumentNullException(nameof(patches));
            if (targetAssemblies == null)
                throw new ArgumentNullException(nameof(targetAssemblies));

            var resolvedPatches = new List<PatchInfo>();
            var assembliesList = targetAssemblies.ToList();

            foreach (var patch in patches)
            {
                // Try to resolve the target type and method in the target assemblies
                var resolvedPatch = ResolveTargetForPatch(patch, assembliesList);
                if (resolvedPatch != null)
                {
                    resolvedPatches.Add(resolvedPatch);
                }
            }

            return resolvedPatches;
        }

        private IEnumerable<PatchInfo> ScanTypeForPatches(TypeDefinition type, AssemblyDefinition assembly)
        {
            var patches = new Dictionary<string, PatchInfo>(); // Key: "TypeName.MethodName"

            // Get type-level HarmonyPatch attributes (these define the target type)
            var typeLevelPatches = GetHarmonyPatchAttributesFromType(type);

            // Get method-level patches
            foreach (var method in type.Methods)
            {
                var methodLevelPatches = GetHarmonyPatchAttributesFromMethod(method);
                
                // Combine type-level and method-level patch information
                var combinedPatches = new List<PatchAttributeInfo>();
                
                if (methodLevelPatches.Any())
                {
                    // Method has its own patch attributes
                    foreach (var methodPatch in methodLevelPatches)
                    {
                        // If method patch doesn't specify target type, inherit from type-level
                        if (string.IsNullOrEmpty(methodPatch.TargetTypeName) && typeLevelPatches.Any())
                        {
                            methodPatch.TargetTypeName = typeLevelPatches[0].TargetTypeName;
                        }
                        combinedPatches.Add(methodPatch);
                    }
                }
                else if (typeLevelPatches.Any())
                {
                    // No method-level patches, use type-level patches
                    combinedPatches.AddRange(typeLevelPatches);
                }

                // Process each combined patch
                foreach (var patchAttr in combinedPatches)
                {
                    if (string.IsNullOrEmpty(patchAttr.TargetTypeName))
                        continue;

                    var key = $"{patchAttr.TargetTypeName}.{patchAttr.MethodName ?? ""}";
                    
                    // Get or create patch info
                    if (!patches.TryGetValue(key, out var patchInfo))
                    {
                        patchInfo = new PatchInfo(null!, null!, patchAttr);
                        patches[key] = patchInfo;
                    }

                    // Add the appropriate patch method
                    if (IsPrefix(method))
                    {
                        patchInfo.Prefix = new PatchMethodInfo(method, type, assembly);
                        AnalyzePatchMethodParameters(patchInfo.Prefix);
                    }
                    else if (IsPostfix(method))
                    {
                        patchInfo.Postfix = new PatchMethodInfo(method, type, assembly);
                        AnalyzePatchMethodParameters(patchInfo.Postfix);
                    }
                    else if (IsFinalizer(method))
                    {
                        patchInfo.Finalizer = new PatchMethodInfo(method, type, assembly);
                        AnalyzePatchMethodParameters(patchInfo.Finalizer);
                    }
                }
            }

            // Return only patches that have at least one patch method
            return patches.Values.Where(p => p.Prefix != null || p.Postfix != null || p.Finalizer != null);
        }

        private List<PatchAttributeInfo> GetHarmonyPatchAttributesFromType(TypeDefinition type)
        {
            var attributes = new List<PatchAttributeInfo>();

            foreach (var attr in type.CustomAttributes)
            {
                if (attr.AttributeType.Name == "HarmonyPatch")
                {
                    var harmonyPatch = ParseHarmonyPatchAttribute(attr);
                    if (harmonyPatch != null)
                    {
                        attributes.Add(harmonyPatch);
                    }
                }
            }

            return attributes;
        }

        private List<PatchAttributeInfo> GetHarmonyPatchAttributesFromMethod(MethodDefinition method)
        {
            var attributes = new List<PatchAttributeInfo>();

            foreach (var attr in method.CustomAttributes)
            {
                if (attr.AttributeType.Name == "HarmonyPatch")
                {
                    var harmonyPatch = ParseHarmonyPatchAttribute(attr);
                    if (harmonyPatch != null)
                    {
                        attributes.Add(harmonyPatch);
                    }
                }
            }

            return attributes;
        }


        private PatchAttributeInfo? ParseHarmonyPatchAttribute(CustomAttribute attr)
        {
            var harmonyPatch = new PatchAttributeInfo();

            // Parse constructor arguments
            if (attr.HasConstructorArguments)
            {
                for (int i = 0; i < attr.ConstructorArguments.Count; i++)
                {
                    var arg = attr.ConstructorArguments[i];
                    
                    if (arg.Type.Name == "Type")
                    {
                        // Argument is the target type
                        if (arg.Value is TypeDefinition targetType)
                        {
                            harmonyPatch.TargetTypeName = targetType.FullName;
                        }
                        else if (arg.Value is TypeReference targetTypeRef)
                        {
                            harmonyPatch.TargetTypeName = targetTypeRef.FullName;
                        }
                    }
                    else if (arg.Type.Name == "String")
                    {
                        // String argument is usually the method name
                        harmonyPatch.MethodName = arg.Value?.ToString();
                    }
                }
            }

            // For now, we only need the basic constructor arguments
            // We can add property parsing later if needed

            return harmonyPatch;
        }

        private bool IsPrefix(MethodDefinition method)
        {
            // Check for HarmonyPrefix attribute
            foreach (var attr in method.CustomAttributes)
            {
                if (attr.AttributeType.Name == "HarmonyPrefixAttribute" || 
                    attr.AttributeType.Name == "HarmonyPrefix")
                {
                    return true;
                }
            }

            // Fallback to naming convention
            return method.Name == "Prefix" || method.Name.EndsWith("Prefix");
        }

        private bool IsPostfix(MethodDefinition method)
        {
            // Check for HarmonyPostfix attribute
            foreach (var attr in method.CustomAttributes)
            {
                if (attr.AttributeType.Name == "HarmonyPostfixAttribute" || 
                    attr.AttributeType.Name == "HarmonyPostfix")
                {
                    return true;
                }
            }

            // Fallback to naming convention
            return method.Name == "Postfix" || method.Name.EndsWith("Postfix");
        }

        private bool IsFinalizer(MethodDefinition method)
        {
            // Check for HarmonyFinalizer attribute
            foreach (var attr in method.CustomAttributes)
            {
                if (attr.AttributeType.Name == "HarmonyFinalizerAttribute" || 
                    attr.AttributeType.Name == "HarmonyFinalizer")
                {
                    return true;
                }
            }

            // Fallback to naming convention
            return method.Name == "Finalizer" || method.Name.EndsWith("Finalizer");
        }

        private void AnalyzePatchMethodParameters(PatchMethodInfo patchMethodInfo)
        {
            // TODO: Analyze the method parameters to determine what arguments need to be passed
            // This includes identifying __instance, __result, original parameters, etc.
            // For now, this is a stub
        }

        private PatchInfo? ResolveTargetForPatch(PatchInfo patch, List<AssemblyDefinition> targetAssemblies)
        {
            if (patch.PatchAttribute.TargetTypeName == null)
                return null;

            // Find the target type in the target assemblies
            TypeDefinition? targetType = null;
            foreach (var assembly in targetAssemblies)
            {
                targetType = FindTypeByName(assembly, patch.PatchAttribute.TargetTypeName);
                if (targetType != null)
                    break;
            }

            if (targetType == null)
                return null;

            // Find the target method
            MethodDefinition? targetMethod = null;
            if (!string.IsNullOrEmpty(patch.PatchAttribute.MethodName))
            {
                targetMethod = FindMethodByName(targetType, patch.PatchAttribute.MethodName);
            }

            if (targetMethod == null)
                return null;

            // Create a new patch info with resolved targets
            var resolvedPatch = new PatchInfo(targetType, targetMethod, patch.PatchAttribute)
            {
                Prefix = patch.Prefix,
                Postfix = patch.Postfix,
                Finalizer = patch.Finalizer
            };

            return resolvedPatch;
        }

        private TypeDefinition? FindTypeByName(AssemblyDefinition assembly, string typeName)
        {
            // Direct lookup
            foreach (var type in assembly.MainModule.Types)
            {
                if (type.FullName == typeName || type.Name == typeName)
                {
                    return type;
                }

                // Check nested types
                var nestedType = FindNestedTypeByName(type, typeName);
                if (nestedType != null)
                    return nestedType;
            }

            return null;
        }

        private TypeDefinition? FindNestedTypeByName(TypeDefinition parentType, string typeName)
        {
            foreach (var nestedType in parentType.NestedTypes)
            {
                if (nestedType.FullName == typeName || nestedType.Name == typeName)
                {
                    return nestedType;
                }

                var deeperNested = FindNestedTypeByName(nestedType, typeName);
                if (deeperNested != null)
                    return deeperNested;
            }

            return null;
        }

        private MethodDefinition? FindMethodByName(TypeDefinition type, string methodName)
        {
            foreach (var method in type.Methods)
            {
                if (method.Name == methodName)
                {
                    return method;
                }
            }

            return null;
        }
    }
}