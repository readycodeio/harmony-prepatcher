using HarmonyWeaver.Core.Interfaces;
using HarmonyWeaver.Core.Loading;
using HarmonyWeaver.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HarmonyWeaver.Core.Implementation
{
    /// <summary>
    /// Service for discovering patches using isolated assembly loading
    /// </summary>
    public class PatchDiscoveryService : IPatchDiscoveryService
    {
        public IEnumerable<SimplePatchInfo> DiscoverPatches(IEnumerable<string> patchAssemblyPaths)
        {
            if (patchAssemblyPaths == null)
                throw new ArgumentNullException(nameof(patchAssemblyPaths));

            var allPatches = new List<SimplePatchInfo>();

            // Use isolated context for patch discovery
            using var discoveryContext = new PatchDiscoveryContext();
            
            try
            {
                // Load all patch assemblies in the isolated context
                foreach (var assemblyPath in patchAssemblyPaths)
                {
                    discoveryContext.LoadAssemblyForDiscovery(assemblyPath);
                }

                // Discover patches
                allPatches.AddRange(discoveryContext.DiscoverPatches());
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to discover patches: {ex.Message}", ex);
            }

            return allPatches;
        }
    }
}