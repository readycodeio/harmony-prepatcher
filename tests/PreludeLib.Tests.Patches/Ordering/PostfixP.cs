using HarmonyLib;
using PreludeLib.Tests.Examples;

namespace PreludeLib.Tests.Patches.Ordering;

/// 36) & 37) P should run BEFORE Q (so Steps shows P then Q)
[HarmonyPatch(typeof(OrderStackTargets), nameof(OrderStackTargets.Compute))]
public static class PostfixP
{
    [HarmonyPostfix, HarmonyPriority(Priority.High)]
    public static void Postfix(ref int __result)
    {
        OrderStackProbes.Steps.Add("P");
        __result = __result * 10 + 4;
    }
}