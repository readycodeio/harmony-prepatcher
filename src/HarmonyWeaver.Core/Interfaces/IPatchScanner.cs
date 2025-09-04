using HarmonyWeaver.Core.Models;
using Mono.Cecil;
using System.Collections.Generic;

namespace HarmonyWeaver.Core.Interfaces
{
    /// <summary>
    /// Interface for scanning assemblies to find HarmonyPatch attributes and extract patch information
    /// </summary>
    public interface IPatchScanner
    {
        /// <summary>
        /// Scan an assembly for classes with HarmonyPatch attributes
        /// </summary>
        /// <param name="assembly">The assembly to scan</param>
        /// <returns>Collection of patch information found in the assembly</returns>
        IEnumerable<PatchInfo> ScanForPatches(AssemblyDefinition assembly);

        /// <summary>
        /// Scan multiple assemblies for classes with HarmonyPatch attributes
        /// </summary>
        /// <param name="assemblies">The assemblies to scan</param>
        /// <returns>Collection of patch information found in all assemblies</returns>
        IEnumerable<PatchInfo> ScanForPatches(IEnumerable<AssemblyDefinition> assemblies);

        /// <summary>
        /// Find target types and methods that match the patch specifications
        /// </summary>
        /// <param name="patches">The patches to resolve targets for</param>
        /// <param name="targetAssemblies">The assemblies to search for target types</param>
        /// <returns>Collection of patches with resolved target information</returns>
        IEnumerable<PatchInfo> ResolveTargets(IEnumerable<PatchInfo> patches, IEnumerable<AssemblyDefinition> targetAssemblies);
    }
}