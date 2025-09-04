using System;
using System.Collections.Generic;

namespace HarmonyWeaver.Core.Models
{
    /// <summary>
    /// Represents the information from a HarmonyPatch attribute
    /// </summary>
    public class HarmonyPatchAttribute
    {
        /// <summary>
        /// The target type name to patch
        /// </summary>
        public string? TargetTypeName { get; set; }

        /// <summary>
        /// The target type (if specified directly)
        /// </summary>
        public Type? TargetType { get; set; }

        /// <summary>
        /// The name of the method to patch
        /// </summary>
        public string? MethodName { get; set; }

        /// <summary>
        /// The method type (Normal, Constructor, StaticConstructor, etc.)
        /// </summary>
        public MethodType MethodType { get; set; } = MethodType.Normal;

        /// <summary>
        /// Parameter types for method overload resolution
        /// </summary>
        public Type[]? ParameterTypes { get; set; }

        /// <summary>
        /// Argument types for method overload resolution (alternative to ParameterTypes)
        /// </summary>
        public string[]? ArgumentTypes { get; set; }

        /// <summary>
        /// Generic method arguments
        /// </summary>
        public Type[]? GenericArguments { get; set; }

        /// <summary>
        /// Method that returns the target type (for dynamic type resolution)
        /// </summary>
        public string? TargetTypeMethod { get; set; }

        /// <summary>
        /// Method that returns the target method (for dynamic method resolution)
        /// </summary>
        public string? TargetMethodMethod { get; set; }

        /// <summary>
        /// Additional properties from the attribute
        /// </summary>
        public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Types of methods that can be patched
    /// </summary>
    public enum MethodType
    {
        Normal,
        Getter,
        Setter,
        Constructor,
        StaticConstructor,
        Enumerator
    }
}