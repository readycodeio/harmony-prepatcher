using System.Reflection;
using HarmonyLib;
using PreludeLib.Runtime.Utils;

namespace PreludeLib.Runtime.DummyBackend;

public class PreludeDummyBackend(string id) : IPreludeBackend
{
    public readonly string Id = id;

    public IPreludePatchProcessor CreateProcessor(MethodBase original)
        => new PreludeDummyPatchProcessor(this, original);

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

    public MethodInfo Patch(
        MethodBase original,
        HarmonyMethod? prefix = null,
        HarmonyMethod? postfix = null,
        HarmonyMethod? finalizer = null,
        HarmonyMethod? transpiler = null)
        => MethodUtils.WrapMethod(original);

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