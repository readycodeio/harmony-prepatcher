using System.Reflection;
using HarmonyLib;

namespace PreludeLib.Runtime.HarmonyBackend;

public class PreludeHarmonyPatchProcessor(PreludeHarmonyBackend instance, MethodBase original) : IPreludePatchProcessor
{
    private readonly PatchProcessor _harmonyProcessor = new(instance.Harmony, original);

    public IPreludePatchProcessor AddPrefix(HarmonyMethod? prefix)
    {
        _harmonyProcessor.AddPrefix(prefix);
        return this;
    }

    public IPreludePatchProcessor AddPostfix(HarmonyMethod? postfix)
    {
        _harmonyProcessor.AddPostfix(postfix);
        return this;
    }

    public IPreludePatchProcessor AddTranspiler(HarmonyMethod? transpiler)
    {
        _harmonyProcessor.AddTranspiler(transpiler);
        return this;
    }

    public IPreludePatchProcessor AddFinalizer(HarmonyMethod? finalizer)
    {
        _harmonyProcessor.AddFinalizer(finalizer);
        return this;
    }
    
    public MethodInfo Patch()
        => _harmonyProcessor.Patch();

    public void Unpatch(HarmonyPatchType patchType)
        => _harmonyProcessor.Unpatch(patchType, instance.Harmony.Id);
}