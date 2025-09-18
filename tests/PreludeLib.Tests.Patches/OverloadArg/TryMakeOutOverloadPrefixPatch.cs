using System.Reflection;
using HarmonyLib;
using PreludeLib.Attributes;
using PreludeLib.Tests.Examples;

namespace PreludeLib.Tests.Patches.OverloadArg;

/// 16) PatchOverloadWithOutArgumentUsingArgumentTypeOut
/// Target OverloadArgTargets.TryMake(out int)
/// Note: at runtime, 'out int' is also a ByRef type; signature resolves by param list.
[HarmonyPatch(typeof(OverloadArgTargets))]
public static class TryMakeOutOverloadPrefixPatch
{
    [HarmonyTargetMethodHint(nameof(OverloadArgTargets.TryMake), [typeof(Out<int>)])]
    static MethodBase TargetMethod()
        => AccessTools.Method(typeof(OverloadArgTargets),
            nameof(OverloadArgTargets.TryMake),
            new[] { typeof(int).MakeByRefType() }); // 'out int' is ByRef

    [HarmonyPrefix]
    public static bool TryMakeOut_Prefix(ref int value, ref bool __result)
    {
        // Skip original and supply our own out value + return value.
        value = 999;
        __result = true;
        return false; // skip original
    }
}