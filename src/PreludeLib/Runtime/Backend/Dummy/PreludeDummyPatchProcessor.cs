using System.Reflection;
using HarmonyLib;
using PreludeLib.Runtime.Utils;

namespace PreludeLib.Runtime.Backend.Dummy;

public class PreludeDummyPatchProcessor : IPreludePatchProcessor
{
    private readonly MethodBase _original;
    
    public PreludeDummyPatchProcessor(PreludeDummyBackend instance, MethodBase original)
    {
        _original = original;
    }

    public void AddPrefix(HarmonyMethod? prefix)
    {
        // no-op
    }

    public void AddPostfix(HarmonyMethod? postfix)
    {
        // no-op
    }

    public void AddTranspiler(HarmonyMethod? transpiler)
    {
        // no-op
    }

    public void AddFinalizer(HarmonyMethod? finalizer)
    {
        // no-op
    }

    public void Patch()
    {
        // no-op
    }
}