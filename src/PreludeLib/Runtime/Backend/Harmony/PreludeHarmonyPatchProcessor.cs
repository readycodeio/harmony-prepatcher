using System.Reflection;
using HarmonyLib;

namespace PreludeLib.Runtime.Backend.HarmonyDetour;

public class PreludeHarmonyPatchProcessor(PreludeHarmonyBackend instance, MethodBase original) : IPreludePatchProcessor
{
    private readonly PatchProcessor _harmonyProcessor = new(instance.Harmony, original);

    public void AddPrefix(HarmonyMethod? prefix)
    {
        _harmonyProcessor.AddPrefix(prefix);
    }

    public void AddPostfix(HarmonyMethod? postfix)
    {
        _harmonyProcessor.AddPostfix(postfix);
    }

    public void AddTranspiler(HarmonyMethod? transpiler)
    {
        _harmonyProcessor.AddTranspiler(transpiler);
    }

    public void AddFinalizer(HarmonyMethod? finalizer)
    {
        _harmonyProcessor.AddFinalizer(finalizer);
    }
    
    public void Patch()
        => _harmonyProcessor.Patch();

    public void Unpatch(HarmonyPatchType patchType)
        => _harmonyProcessor.Unpatch(patchType, instance.Harmony.Id);
}