using System.Reflection;
using HarmonyLib;

namespace PreludeLib.Runtime.Backend.WeaverCallback;

[AttributeUsage(AttributeTargets.Event)]
public class WeaverCallbackAttribute : Attribute
{
    private readonly SerializedMethodInfo _original;
    private readonly SerializedMethodInfo _patch;

    public readonly HarmonyPatchType PatchType;
    public readonly string? Category;

    public MethodBase? GetOriginalMethod(string? alcName)
        => _original.GetMethodBase(alcName);

    public MethodInfo? GetPatchMethod(string? alcName)
        => _patch.GetMethod(alcName);

    private WeaverCallbackAttribute(SerializedMethodInfo original, SerializedMethodInfo patch, HarmonyPatchType patchType, string? category)
    {
        _original = original;
        _patch = patch;
        PatchType = patchType;
        Category = category;
    }
    
    public WeaverCallbackAttribute(
        string moduleGUID, 
        int methodToken,
        string patchModuleGUID, 
        int patchMethodToken,
        HarmonyPatchType patchType,
        string? category)
        : this(new SerializedMethodInfo(moduleGUID, methodToken), 
            new SerializedMethodInfo(patchModuleGUID, patchMethodToken), patchType, category)
    {
        // empty
    }
}
