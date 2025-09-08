using HarmonyLib;
using PreludeLib.Tests.Examples;

namespace PreludeLib.Tests.Patches.Ordering;

/// 33) & 34) continued
[HarmonyPatch(typeof(OrderStackTargets), nameof(OrderStackTargets.Compute))]
public static class PrefixLow_B
{
    [HarmonyPrefix, HarmonyPriority(Priority.Low)]
    public static void Prefix(ref int x)
    {
        OrderStackProbes.Steps.Add("B");
        x = x * 10 + 2;
    }
}