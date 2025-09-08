using System;
using HarmonyLib;

namespace PreludeLib.Runtime.HarmonyBackend;

public class PreludeHarmonyClassProcessor : IPreludeClassProcessor
{
    private readonly PreludeHarmonyBackend _instance;
    private readonly PatchClassProcessor _harmonyProcessor;
    
    public PreludeHarmonyClassProcessor(PreludeHarmonyBackend instance, Type type)
    {
        _instance = instance;
        _harmonyProcessor = new PatchClassProcessor(instance.Harmony, type);
    }

    public void Patch()
    {
        _harmonyProcessor.Patch();
    }
}