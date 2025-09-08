using System;
using System.Reflection;
using HarmonyLib;

namespace PreludeLib.Runtime.DummyBackend;

public class PreludeDummyBackend(string id) : IPreludeBackend
{
    public readonly string Id = id;
    
    public IPreludeClassProcessor CreateClassProcessor(Type type)
        => new PreludeDummyClassProcessor(this, type);

    public void PatchAll(Assembly patchAssembly)
    {
        // no-op
    }

    public void PatchCategory(Assembly patchAssembly, string category)
    {
        // no-op
    }

    public void PatchAllUncategorized(Assembly patchAssembly)
    {
        // no-op
    }

    public void Patch(
        MethodInfo original, 
        PreludeMethod? prefix = null,
        PreludeMethod? postfix = null,
        PreludeMethod? finalizer = null,
        PreludeMethod? transpiler = null)
    {
        // no-op
    }

    public void UnpatchAll()
    {
        // no-op
    }

    public void UnpatchAll(Assembly patchAssembly)
    {
        // no-op
    }

    public void UnpatchCategory(Assembly patchAssembly, string category)
    {
        // no-op
    }

    public void UnpatchUncategorized(Assembly patchAssembly)
    {
        // no-op
    }

    public void UnpatchCategory(string category)
    {
        // no-op
    }

    public void UnpatchUncategorized()
    {
        // no-op
    }

    public void Unpatch(MethodBase original, HarmonyPatchType patchType)
    {
        // no-op
    }

    public void Unpatch(MethodBase original, MethodInfo patch)
    {
        // no-op
    }
}