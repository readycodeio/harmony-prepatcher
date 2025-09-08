using System.Reflection;
using HarmonyLib;
using PreludeLib.Tests.Examples;

namespace PreludeLib.Tests.Patches.OverloadArg;

/// 15) PatchOverloadWithByRefArgumentUsingArgumentTypeRef
/// Target OverloadArgTargets.Inc(ref int)
[HarmonyPatch(typeof(OverloadArgTargets))]
public static class IncRefOverloadPostfixPatch
{
    static MethodBase TargetMethod()
        => AccessTools.Method(typeof(OverloadArgTargets),
            nameof(OverloadArgTargets.Inc),
            new[] { typeof(int).MakeByRefType() });
    
    [HarmonyPostfix]
    public static void IncRef_Postfix(ref int __result)
    {
        // Make a visible change so we know this overload was hit.
        __result += 1000;
    }
}