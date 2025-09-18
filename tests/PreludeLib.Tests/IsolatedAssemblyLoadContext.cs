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
        "Mono.Cecil",
    };

    private static readonly string[] Main =
    {
        "PreludeLib.Tests.Payload",
        "PreludeLib.Tests.Patches",
        "PreludeLib.Tests.Examples",
    };

    private readonly AssemblyDependencyResolver _mainResolver;
    private readonly AssemblyDependencyResolver _commonResolver;

    public IsolatedAssemblyLoadContext(string mainAssemblyPath, string commonAssemblyPath)
        : base(isCollectible: true, name: Guid.NewGuid().ToString())
    {
        _mainResolver = new AssemblyDependencyResolver(mainAssemblyPath);
        _commonResolver = new AssemblyDependencyResolver(commonAssemblyPath);
        
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

        if (Main.Contains(assemblyName.Name!, StringComparer.Ordinal))
        {
            var path = _mainResolver.ResolveAssemblyToPath(assemblyName);
            if (path != null)
                return LoadFromAssemblyPath(path);
        }
        else
        {
            var path = _commonResolver.ResolveAssemblyToPath(assemblyName);
            if (path != null)
                return LoadFromAssemblyPath(path);
        }
        
        return null;
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        if (Main.Contains(unmanagedDllName, StringComparer.Ordinal))
        {
            var path = _mainResolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            if (path != null)
                return LoadUnmanagedDllFromPath(path);
        }
        else
        {
            var path = _commonResolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            if (path != null)
                return LoadUnmanagedDllFromPath(path);
        }

        return IntPtr.Zero;
    }
}