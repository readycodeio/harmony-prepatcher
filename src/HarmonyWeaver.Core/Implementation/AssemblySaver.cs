using HarmonyWeaver.Core.Interfaces;
using Mono.Cecil;
using System;
using System.IO;

namespace HarmonyWeaver.Core.Implementation
{
    /// <summary>
    /// Default implementation of IAssemblySaver
    /// </summary>
    public class AssemblySaver : IAssemblySaver
    {
        public void SaveAssembly(AssemblyDefinition assembly, string outputPath)
        {
            if (assembly == null)
                throw new ArgumentNullException(nameof(assembly));

            if (string.IsNullOrWhiteSpace(outputPath))
                throw new ArgumentNullException(nameof(outputPath));

            try
            {
                // Ensure the output directory exists
                var directory = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Write the assembly
                assembly.Write(outputPath);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to save assembly to {outputPath}: {ex.Message}", ex);
            }
        }

        public string GenerateOutputPath(string originalPath, string suffix = "_patched")
        {
            if (string.IsNullOrWhiteSpace(originalPath))
                throw new ArgumentNullException(nameof(originalPath));

            var directory = Path.GetDirectoryName(originalPath) ?? "";
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(originalPath);
            var extension = Path.GetExtension(originalPath);

            return Path.Combine(directory, $"{fileNameWithoutExtension}{suffix}{extension}");
        }
    }
}