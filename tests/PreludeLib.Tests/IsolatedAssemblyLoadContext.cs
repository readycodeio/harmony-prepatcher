using System.Reflection;
using System.Runtime.Loader;

namespace PreludeLib.Tests;

public sealed class IsolatedAssemblyLoadContext : AssemblyLoadContext
{
    private static readonly string[] Shared =
    {
        "0Harmony",
        "HarmonyLib",
        "MonoMod.RuntimeDetour",
        "MonoMod.Utils",
        "Mono.Cecil" // if present
    };
    
    private readonly AssemblyDependencyResolver _resolver;

    public IsolatedAssemblyLoadContext(string mainAssemblyPath)
        : base(isCollectible: true, name: Guid.NewGuid().ToString())
    {
        _resolver = new AssemblyDependencyResolver(mainAssemblyPath);
        
        Resolving += (_, name) =>
        {
            if (Shared.Contains(name.Name, StringComparer.Ordinal))
                return AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == name.Name);
            return null;
        };
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (Shared.Contains(assemblyName.Name!, StringComparer.Ordinal))
            return null; // force Resolving -> return Default copy

        var path = _resolver.ResolveAssemblyToPath(assemblyName);
        if (path != null)
            return LoadFromAssemblyPath(path);
        return null;
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return path != null ? LoadUnmanagedDllFromPath(path) : IntPtr.Zero;
    }
}