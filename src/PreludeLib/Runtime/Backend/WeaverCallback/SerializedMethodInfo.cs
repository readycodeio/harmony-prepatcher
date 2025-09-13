using System.Diagnostics.Contracts;
using System.Reflection;
using HarmonyLib;

namespace PreludeLib.Runtime.Backend.WeaverCallback;

[Serializable]
public struct SerializedMethodInfo(string moduleGuid, int methodToken)
{
    private string _moduleGUID = moduleGuid;
    private int _methodToken = methodToken;
    
    public SerializedMethodInfo(MethodBase method)
        : this(method.Module.ModuleVersionId.ToString(), method.MetadataToken)
    {
        // empty
    }

    [Pure] public MethodBase? GetMethodBase(string? alcName)
        => AccessTools.GetMethodByModuleAndToken(_moduleGUID, _methodToken, alcName);
    
    [Pure] public MethodInfo? GetMethod(string? alcName)
        => AccessTools.GetMethodByModuleAndToken(_moduleGUID, _methodToken, alcName);
}