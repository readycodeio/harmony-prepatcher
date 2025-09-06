using HarmonyWeaver.Core.Models;
using Mono.Cecil;
using System.Collections.Generic;

namespace HarmonyWeaver.Core.Interfaces
{
    /// <summary>
    /// Interface for weaving IL code to apply Harmony-style patches
    /// </summary>
    public interface IILWeaver
    {
        /// <summary>
        /// Apply patches to a target assembly by modifying its IL code
        /// </summary>
        /// <param name="targetAssembly">The assembly to modify</param>
        /// <param name="patches">The patches to apply</param>
        void ApplyPatches(AssemblyDefinition targetAssembly, IEnumerable<PatchInfo> patches);

        /// <summary>
        /// Apply a single patch to a target method
        /// </summary>
        /// <param name="targetMethod">The method to modify</param>
        /// <param name="patch">The patch to apply</param>
        void ApplyPatch(MethodDefinition targetMethod, PatchInfo patch);

        /// <summary>
        /// Weave a prefix method call into the target method
        /// </summary>
        /// <param name="targetMethod">The method to modify</param>
        /// <param name="prefixInfo">Information about the prefix method</param>
        void WeavePrefix(MethodDefinition targetMethod, PatchMethodInfo prefixInfo);

        /// <summary>
        /// Weave a postfix method call into the target method
        /// </summary>
        /// <param name="targetMethod">The method to modify</param>
        /// <param name="postfixInfo">Information about the postfix method</param>
        void WeavePostfix(MethodDefinition targetMethod, PatchMethodInfo postfixInfo);

        /// <summary>
        /// Weave a finalizer method call into the target method
        /// </summary>
        /// <param name="targetMethod">The method to modify</param>
        /// <param name="finalizerInfo">Information about the finalizer method</param>
        void WeaveFinalizer(MethodDefinition targetMethod, PatchMethodInfo finalizerInfo);
    }
}