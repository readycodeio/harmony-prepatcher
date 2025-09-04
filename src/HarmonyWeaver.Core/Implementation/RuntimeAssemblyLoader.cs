using HarmonyWeaver.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace HarmonyWeaver.Core.Implementation
{
	/// <summary>
	/// Loads assemblies into isolated AssemblyLoadContexts to avoid binding to already loaded assemblies.
	/// </summary>
	public class RuntimeAssemblyLoader : IRuntimeAssemblyLoader
	{
		public AssemblyLoadContext CreateContext(string name, IEnumerable<string> probingPaths, bool isCollectible = true, bool preferDefaultLoad = false)
		{
			return new IsolatedAssemblyLoadContext(name, probingPaths, isCollectible, preferDefaultLoad);
		}

		public Assembly LoadFromPath(AssemblyLoadContext context, string assemblyPath)
		{
			if (context == null) throw new ArgumentNullException(nameof(context));
			if (string.IsNullOrWhiteSpace(assemblyPath)) throw new ArgumentNullException(nameof(assemblyPath));
			var fullPath = Path.GetFullPath(assemblyPath);
			if (!File.Exists(fullPath)) throw new FileNotFoundException($"Assembly not found: {fullPath}");
			return context.LoadFromAssemblyPath(fullPath);
		}

		public void Unload(AssemblyLoadContext context)
		{
			if (context == null) return;
			context.Unload();
		}
	}
}
