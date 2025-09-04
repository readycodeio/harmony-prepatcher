using HarmonyWeaver.Core.Interfaces;
using HarmonyWeaver.Core.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HarmonyWeaver.Core
{
    /// <summary>
    /// Main implementation of the Harmony weaving process
    /// </summary>
    public class HarmonyWeaver : IHarmonyWeaver, IDisposable
    {
        private readonly IAssemblyLoader _assemblyLoader;
        private readonly IPatchScanner _patchScanner;
        private readonly IILWeaver _ilWeaver;
        private readonly IAssemblySaver _assemblySaver;

        public HarmonyWeaver(
            IAssemblyLoader assemblyLoader,
            IPatchScanner patchScanner,
            IILWeaver ilWeaver,
            IAssemblySaver assemblySaver)
        {
            _assemblyLoader = assemblyLoader ?? throw new ArgumentNullException(nameof(assemblyLoader));
            _patchScanner = patchScanner ?? throw new ArgumentNullException(nameof(patchScanner));
            _ilWeaver = ilWeaver ?? throw new ArgumentNullException(nameof(ilWeaver));
            _assemblySaver = assemblySaver ?? throw new ArgumentNullException(nameof(assemblySaver));
        }

        public IEnumerable<string> ProcessPatches(
            IEnumerable<string> patchAssemblyPaths,
            IEnumerable<string> targetAssemblyPaths,
            string outputDirectory)
        {
            if (patchAssemblyPaths == null) throw new ArgumentNullException(nameof(patchAssemblyPaths));
            if (targetAssemblyPaths == null) throw new ArgumentNullException(nameof(targetAssemblyPaths));
            if (string.IsNullOrWhiteSpace(outputDirectory)) throw new ArgumentNullException(nameof(outputDirectory));

            // Generate output paths based on target assembly names
            var outputPaths = targetAssemblyPaths.Select(path => 
            {
                var fileName = Path.GetFileNameWithoutExtension(path);
                var extension = Path.GetExtension(path);
                return Path.Combine(outputDirectory, $"{fileName}_patched{extension}");
            });

            return ProcessPatches(patchAssemblyPaths, targetAssemblyPaths, outputPaths);
        }

        public IEnumerable<string> ProcessPatches(
            IEnumerable<string> patchAssemblyPaths,
            IEnumerable<string> targetAssemblyPaths,
            IEnumerable<string> outputPaths)
        {
            if (patchAssemblyPaths == null) throw new ArgumentNullException(nameof(patchAssemblyPaths));
            if (targetAssemblyPaths == null) throw new ArgumentNullException(nameof(targetAssemblyPaths));
            if (outputPaths == null) throw new ArgumentNullException(nameof(outputPaths));

            var patchPaths = patchAssemblyPaths.ToList();
            var targetPaths = targetAssemblyPaths.ToList();
            var outPaths = outputPaths.ToList();

            if (targetPaths.Count != outPaths.Count)
                throw new ArgumentException("Number of target assemblies must match number of output paths");

            var result = new List<string>();

            try
            {
                // Step 1: Load patch assemblies
                var patchAssemblies = _assemblyLoader.LoadAssemblies(patchPaths).ToList();
                
                // Step 2: Scan for patches
                var patches = _patchScanner.ScanForPatches(patchAssemblies).ToList();
                
                if (!patches.Any())
                {
                    throw new InvalidOperationException("No HarmonyPatch attributes found in the provided patch assemblies");
                }

                // Step 3: Process each target assembly
                for (int i = 0; i < targetPaths.Count; i++)
                {
                    var targetPath = targetPaths[i];
                    var outputPath = outPaths[i];

                    // Load target assembly
                    var targetAssembly = _assemblyLoader.LoadAssembly(targetPath);
                    
                    // Resolve patch targets for this assembly
                    var resolvedPatches = _patchScanner.ResolveTargets(patches, new[] { targetAssembly }).ToList();
                    
                    if (resolvedPatches.Any())
                    {
                        // Apply patches
                        _ilWeaver.ApplyPatches(targetAssembly, resolvedPatches);
                        
                        // Save modified assembly
                        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
                        _assemblySaver.SaveAssembly(targetAssembly, outputPath);
                        
                        result.Add(outputPath);
                    }
                    else
                    {
                        // No patches found for this assembly, optionally copy original
                        // For now, we'll skip assemblies with no matching patches
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error processing patches: {ex.Message}", ex);
            }
        }

        public void Dispose()
        {
            _assemblyLoader?.Dispose();
        }
    }
}