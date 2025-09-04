using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

namespace HarmonyWeaver.Core.Implementation
{
	/// <summary>
	/// An AssemblyLoadContext with simple probing path resolution.
	/// </summary>
	internal class IsolatedAssemblyLoadContext : AssemblyLoadContext
	{
		private readonly List<string> _probingPaths;
		private readonly bool _preferDefaultLoad;

		public IsolatedAssemblyLoadContext(string name, IEnumerable<string> probingPaths, bool isCollectible, bool preferDefaultLoad)
			: base(name, isCollectible)
		{
			_probingPaths = probingPaths?.Where(p => !string.IsNullOrWhiteSpace(p)).Select(Path.GetFullPath).Distinct().ToList() ?? new List<string>();
			_preferDefaultLoad = preferDefaultLoad;
		}

		protected override Assembly? Load(AssemblyName assemblyName)
		{
			// Optionally prefer default context to avoid duplicate loads of framework assemblies
			if (_preferDefaultLoad)
			{
				var alreadyLoaded = Default.Assemblies.FirstOrDefault(a => string.Equals(a.GetName().Name, assemblyName.Name, StringComparison.OrdinalIgnoreCase));
				if (alreadyLoaded != null)
					return alreadyLoaded;
			}

			foreach (var path in _probingPaths)
			{
				var candidate = Path.Combine(path, assemblyName.Name + ".dll");
				if (File.Exists(candidate))
				{
					try
					{
						return LoadFromAssemblyPath(candidate);
					}
					catch
					{
						// Continue probing other locations
					}
				}
			}

			return null;
		}
	}
}
