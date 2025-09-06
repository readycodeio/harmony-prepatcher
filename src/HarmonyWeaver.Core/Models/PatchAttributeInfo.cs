namespace HarmonyWeaver.Core.Models
{
    /// <summary>
    /// Simple information extracted from HarmonyLib.HarmonyPatch attributes
    /// </summary>
    public class PatchAttributeInfo
    {
        /// <summary>
        /// The target type name to patch
        /// </summary>
        public string? TargetTypeName { get; set; }

        /// <summary>
        /// The name of the method to patch
        /// </summary>
        public string? MethodName { get; set; }

        public PatchAttributeInfo(string? targetTypeName = null, string? methodName = null)
        {
            TargetTypeName = targetTypeName;
            MethodName = methodName;
        }
    }
}