using System.Collections.Generic;

namespace HarmonyWeaver.Core.Interfaces
{
    /// <summary>
    /// Main interface for the Harmony weaving process
    /// </summary>
    public interface IHarmonyWeaver
    {
        /// <summary>
        /// Process patch assemblies and target assemblies to apply patches
        /// </summary>
        /// <param name="patchAssemblyPaths">Paths to assemblies containing patches</param>
        /// <param name="targetAssemblyPaths">Paths to assemblies to be patched</param>
        /// <param name="outputDirectory">Directory where to save patched assemblies</param>
        /// <returns>Collection of paths to the generated patched assemblies</returns>
        IEnumerable<string> ProcessPatches(
            IEnumerable<string> patchAssemblyPaths, 
            IEnumerable<string> targetAssemblyPaths, 
            string outputDirectory);

        /// <summary>
        /// Process patch assemblies and target assemblies to apply patches with custom output names
        /// </summary>
        /// <param name="patchAssemblyPaths">Paths to assemblies containing patches</param>
        /// <param name="targetAssemblyPaths">Paths to assemblies to be patched</param>
        /// <param name="outputPaths">Custom output paths for each target assembly</param>
        /// <returns>Collection of paths to the generated patched assemblies</returns>
        IEnumerable<string> ProcessPatches(
            IEnumerable<string> patchAssemblyPaths,
            IEnumerable<string> targetAssemblyPaths,
            IEnumerable<string> outputPaths);
    }
}