using Mono.Cecil;

namespace HarmonyWeaver.Core.Interfaces
{
    /// <summary>
    /// Interface for saving modified assemblies
    /// </summary>
    public interface IAssemblySaver
    {
        /// <summary>
        /// Save a modified assembly to a file
        /// </summary>
        /// <param name="assembly">The assembly to save</param>
        /// <param name="outputPath">The path where to save the assembly</param>
        void SaveAssembly(AssemblyDefinition assembly, string outputPath);

        /// <summary>
        /// Generate an output path for a patched assembly based on the original path
        /// </summary>
        /// <param name="originalPath">The original assembly path</param>
        /// <param name="suffix">Optional suffix to add (defaults to "_patched")</param>
        /// <returns>The generated output path</returns>
        string GenerateOutputPath(string originalPath, string suffix = "_patched");
    }
}