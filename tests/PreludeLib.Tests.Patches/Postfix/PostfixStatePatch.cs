 using HarmonyLib;
using PreludeLib.Tests.Examples;

namespace PreludeLib.Tests.Patches.Postfix;

/// 7) PostfixReceivesStateFromPrefixVia__state
[HarmonyPatch(typeof(PostfixTargets))]
public static class PostfixStatePatch
{
    [HarmonyPatch(nameof(PostfixTargets.Echo))]
    [HarmonyPrefix]
    public static void EchoPrefix(int v, out int __state)
    {
        // Provide state to postfix — something derived from args
        __state = v * 10;
    }

    [HarmonyPatch(nameof(PostfixTargets.Echo))]
    [HarmonyPostfix]
    public static void EchoPostfix(int v, int __state, ref int __result)
    {
        // Use __state in postfix; result becomes v + (v*10)
        __result += __state;
    }
}