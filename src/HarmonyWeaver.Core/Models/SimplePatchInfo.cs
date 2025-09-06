namespace HarmonyWeaver.Core.Models
{
    /// <summary>
    /// Simplified patch information for comparing Cecil vs Reflection discovery
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
        /// Name of the postfix method, if any
        /// </summary>
        public string? PostfixMethodName { get; set; }

        /// <summary>
        /// Name of the finalizer method, if any
        /// </summary>
        public string? FinalizerMethodName { get; set; }

        /// <summary>
        /// Check if this patch has any patch methods
        /// </summary>
        public bool HasAnyPatchMethods => 
            !string.IsNullOrEmpty(PrefixMethodName) || 
            !string.IsNullOrEmpty(PostfixMethodName) || 
            !string.IsNullOrEmpty(FinalizerMethodName);

        /// <summary>
        /// Create a key for comparison purposes
        /// </summary>
        public string GetKey() => $"{TargetTypeName}.{TargetMethodName}";

        public override string ToString() => 
            $"{GetKey()} (Prefix: {PrefixMethodName}, Postfix: {PostfixMethodName}, Finalizer: {FinalizerMethodName})";
    }
}