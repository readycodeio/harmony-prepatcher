 using HarmonyLib;
using PreludeLib.Tests.Examples;

namespace PreludeLib.Tests.Patches.Ordering;

/// 36) & 37) continued
[HarmonyPatch(typeof(OrderStackTargets), nameof(OrderStackTargets.Compute))]
public static class PostfixQ
{
    [HarmonyPostfix]
    public static void Postfix(ref int __result)
    {
        OrderStackProbes.Steps.Add("Q");
        __result = __result * 10 + 5;
    }
}