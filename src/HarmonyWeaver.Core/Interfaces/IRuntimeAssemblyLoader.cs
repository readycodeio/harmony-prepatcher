using System.Reflection;
using System.Runtime.Loader;

namespace HarmonyWeaver.Core.Interfaces
{
	/// <summary>
	/// Provides APIs to load assemblies into isolated AssemblyLoadContext instances
	/// to avoid name collisions between original and patched assemblies.
	/// </summary>
	public interface IRuntimeAssemblyLoader
	{
		/// <summary>
		/// Create a new isolated AssemblyLoadContext.
		/// </summary>
		/// <param name="name">Logical name for the context.</param>
		/// <param name="probingPaths">Directories to probe when resolving dependencies.</param>
		/// <param name="isCollectible">Whether the context is unloadable.</param>
		/// <param name="preferDefaultLoad">If true, prefer already loaded assemblies from default context.</param>
		/// <returns>The created AssemblyLoadContext.</returns>
		AssemblyLoadContext CreateContext(string name, IEnumerable<string> probingPaths, bool isCollectible = true, bool preferDefaultLoad = false);

		/// <summary>
		/// Load an assembly file into the specified context.
		/// </summary>
		/// <param name="context">Target load context.</param>
		/// <param name="assemblyPath">Full path to the assembly file.</param>
		/// <returns>The loaded runtime Assembly.</returns>
		Assembly LoadFromPath(AssemblyLoadContext context, string assemblyPath);

		/// <summary>
		/// Attempt to unload the provided context. Caller should force a GC if needed.
		/// </summary>
		/// <param name="context">The context to unload.</param>
		void Unload(AssemblyLoadContext context);
	}
}
