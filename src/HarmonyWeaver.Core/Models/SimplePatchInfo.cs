using System.Collections.Generic;

namespace HarmonyWeaver.Core.Models
{
    /// <summary>
    /// Simplified patch information discovered via reflection (not Cecil)
    /// Used to guide the Cecil-based patching process
    /// </summary>
    public class SimplePatchInfo
    {
        /// <summary>
        /// Full name of the target type to patch
        /// </summary>
        public string TargetTypeName { get; set; } = "";

        /// <summary>
        /// Name of the target method to patch
        /// </summary>
        public string? TargetMethodName { get; set; }

        /// <summary>
        /// Path to the assembly containing the patch
        /// </summary>
        public string PatchAssemblyPath { get; set; } = "";

        /// <summary>
        /// Full name of the type containing the patch methods
        /// </summary>
        public string PatchTypeName { get; set; } = "";

        /// <summary>
        /// Name of the prefix method, if any
        /// </summary>
        public string? PrefixMethodName { get; set; }

        /// <summary>
        /// Whether the prefix method returns bool (for skip logic)
        /// </summary>
        public bool PrefixReturnsBool { get; set; }

        /// <summary>
        /// Parameters of the prefix method
        /// </summary>
        public List<SimplePatchParameterInfo> PrefixParameters { get; set; } = new List<SimplePatchParameterInfo>();

        /// <summary>
        /// Name of the postfix method, if any
        /// </summary>
        public string? PostfixMethodName { get; set; }

        /// <summary>
        /// Parameters of the postfix method
        /// </summary>
        public List<SimplePatchParameterInfo> PostfixParameters { get; set; } = new List<SimplePatchParameterInfo>();

        /// <summary>
        /// Name of the finalizer method, if any
        /// </summary>
        public string? FinalizerMethodName { get; set; }

        /// <summary>
        /// Parameters of the finalizer method
        /// </summary>
        public List<SimplePatchParameterInfo> FinalizerParameters { get; set; } = new List<SimplePatchParameterInfo>();

        /// <summary>
        /// Check if this patch has any patch methods
        /// </summary>
        public bool HasAnyPatchMethods => 
            !string.IsNullOrEmpty(PrefixMethodName) || 
            !string.IsNullOrEmpty(PostfixMethodName) || 
            !string.IsNullOrEmpty(FinalizerMethodName);
    }

    /// <summary>
    /// Simplified parameter information for patch methods
    /// </summary>
    public class SimplePatchParameterInfo
    {
        /// <summary>
        /// Parameter name
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        /// Full type name
        /// </summary>
        public string TypeName { get; set; } = "";

        /// <summary>
        /// Whether this is a by-reference parameter
        /// </summary>
        public bool IsByReference { get; set; }

        /// <summary>
        /// Determine the kind of parameter based on name and type
        /// </summary>
        public PatchParameterKind GetParameterKind()
        {
            return Name switch
            {
                "__instance" => PatchParameterKind.Instance,
                "__result" => PatchParameterKind.Result,
                "__originalMethod" => PatchParameterKind.OriginalMethod,
                "__args" => PatchParameterKind.Arguments,
                "__exception" => PatchParameterKind.Exception,
                "__state" => PatchParameterKind.State,
                "__runOriginal" => PatchParameterKind.RunOriginal,
                _ => PatchParameterKind.OriginalParameter
            };
        }
    }
}