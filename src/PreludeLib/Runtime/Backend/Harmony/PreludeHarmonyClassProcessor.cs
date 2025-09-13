using System.Reflection;
using HarmonyLib;

namespace PreludeLib.Runtime.Backend.HarmonyDetour;

public class PreludeHarmonyClassProcessor(PreludeHarmonyBackend instance, Type type) : IPreludeClassProcessor
{
    private readonly PreludeHarmonyBackend _instance = instance;
    private readonly PatchClassProcessor _harmonyProcessor = new(instance.Harmony, type);
    
    public string? Category
        => _harmonyProcessor.Category;

    public void Patch()
        => _harmonyProcessor.Patch();
}