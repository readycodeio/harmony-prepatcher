using System.Reflection;
using HarmonyLib;
using Microsoft.Extensions.Logging;

namespace PreludeLib.Runtime.Backend;

internal struct AuxiliaryMethodCallContext(Harmony harmony, Type? containerType, MethodBase? original, MethodInfo? patchMethod, ILogger logger)
{
    public readonly Harmony HarmonyInstance = harmony;
    public readonly Type? ContainerType = containerType;
    public readonly MethodBase? Original = original;
    public MethodInfo? PatchMethod = patchMethod;

    public readonly ILogger Logger = logger;
}