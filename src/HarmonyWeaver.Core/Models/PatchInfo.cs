using Mono.Cecil;
using System.Collections.Generic;

namespace HarmonyWeaver.Core.Models
{
    /// <summary>
    /// Represents information about a Harmony patch including target method and patch methods
    /// </summary>
    public class PatchInfo
    {
        /// <summary>
        /// The target type that contains the method to be patched
        /// </summary>
        public TypeDefinition TargetType { get; set; }

        /// <summary>
        /// The target method to be patched
        /// </summary>
        public MethodDefinition TargetMethod { get; set; }

        /// <summary>
        /// Information about the Prefix method, if any
        /// </summary>
        public PatchMethodInfo? Prefix { get; set; }

        /// <summary>
        /// Information about the Postfix method, if any
        /// </summary>
        public PatchMethodInfo? Postfix { get; set; }

        /// <summary>
        /// Information about the Finalizer method, if any
        /// </summary>
        public PatchMethodInfo? Finalizer { get; set; }

        /// <summary>
        /// Information about the HarmonyPatch attribute that defined this patch
        /// </summary>
        public PatchAttributeInfo PatchAttribute { get; set; }

        public PatchInfo(TypeDefinition targetType, MethodDefinition targetMethod, PatchAttributeInfo patchAttribute)
        {
            TargetType = targetType;
            TargetMethod = targetMethod;
            PatchAttribute = patchAttribute;
        }
    }

    /// <summary>
    /// Information about a specific patch method (Prefix, Postfix, or Finalizer)
    /// </summary>
    public class PatchMethodInfo
    {
        /// <summary>
        /// The patch method definition
        /// </summary>
        public MethodDefinition Method { get; set; }

        /// <summary>
        /// The type that contains the patch method
        /// </summary>
        public TypeDefinition DeclaringType { get; set; }

        /// <summary>
        /// The assembly that contains the patch method
        /// </summary>
        public AssemblyDefinition Assembly { get; set; }

        /// <summary>
        /// Parameters that this patch method expects (e.g., __instance, __result, original parameters)
        /// </summary>
        public List<PatchParameterInfo> Parameters { get; set; } = new List<PatchParameterInfo>();

        public PatchMethodInfo(MethodDefinition method, TypeDefinition declaringType, AssemblyDefinition assembly)
        {
            Method = method;
            DeclaringType = declaringType;
            Assembly = assembly;
        }
    }

    /// <summary>
    /// Information about a parameter expected by a patch method
    /// </summary>
    public class PatchParameterInfo
    {
        /// <summary>
        /// The name of the parameter
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The type of the parameter
        /// </summary>
        public TypeReference Type { get; set; }

        /// <summary>
        /// The kind of parameter (original parameter, __instance, __result, etc.)
        /// </summary>
        public PatchParameterKind Kind { get; set; }

        /// <summary>
        /// Index of the original parameter if this represents an original method parameter
        /// </summary>
        public int? OriginalParameterIndex { get; set; }

        public PatchParameterInfo(string name, TypeReference type, PatchParameterKind kind)
        {
            Name = name;
            Type = type;
            Kind = kind;
        }
    }

    /// <summary>
    /// Types of parameters that can be passed to patch methods
    /// </summary>
    public enum PatchParameterKind
    {
        /// <summary>
        /// Original method parameter
        /// </summary>
        OriginalParameter,

        /// <summary>
        /// The instance being called (__instance)
        /// </summary>
        Instance,

        /// <summary>
        /// The result of the method (__result)
        /// </summary>
        Result,

        /// <summary>
        /// Reference to the original method (__originalMethod)
        /// </summary>
        OriginalMethod,

        /// <summary>
        /// Array of all original arguments (__args)
        /// </summary>
        Arguments,

        /// <summary>
        /// Exception thrown by the original method (__exception)
        /// </summary>
        Exception,

        /// <summary>
        /// State object for communication between patches (__state)
        /// </summary>
        State,

        /// <summary>
        /// Run original method flag (__runOriginal)
        /// </summary>
        RunOriginal
    }
}