using HarmonyWeaver.Core.Models;
using System.Collections.Generic;

namespace HarmonyWeaver.Core.Interfaces
{
    /// <summary>
    /// Service for discovering patches in isolated contexts
    /// </summary>
    public interface IPatchDiscoveryService
    {
        /// <summary>
        /// Discover patches from assemblies using isolated loading
        /// </summary>
        /// <param name="patchAssemblyPaths">Paths to assemblies containing patches</param>
        /// <returns>Collection of discovered patch information</returns>
        IEnumerable<SimplePatchInfo> DiscoverPatches(IEnumerable<string> patchAssemblyPaths);
    }
}