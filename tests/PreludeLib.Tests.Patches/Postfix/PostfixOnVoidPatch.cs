using HarmonyLib;
using PreludeLib.Tests.Examples;

namespace PreludeLib.Tests.Patches.Postfix;

/// 6) PostfixOnVoidMethodExecutes
[HarmonyPatch(typeof(PostfixTargets))]
public static class PostfixOnVoidPatch
{
    [HarmonyPatch(nameof(PostfixTargets.NoOp))]
    [HarmonyPostfix]
    public static void NoOpPostfix()
    {
        PostfixProbes.VoidPostfixExecuted = true;
    }
}