using HarmonyLib;
using PreludeLib.Tests.Examples;

namespace PreludeLib.Tests.Patches.Postfix;

/// 5) PostfixCanReadAndModify__result
[HarmonyPatch(typeof(PostfixTargets))]
public static class PostfixModifyResultPatch
{
    [HarmonyPatch(nameof(PostfixTargets.Double))]
    [HarmonyPostfix]
    public static void DoublePostfix(int x, ref int __result)
    {
        // Original: 2*x; Postfix: add +5 to show modification is applied
        __result += 5;
    }
}