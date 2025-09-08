using HarmonyLib;
using PreludeLib.Tests.Examples;

namespace PreludeLib.Tests.Patches.Ordering;

/// 33) & 34) Priority-only prefixes (used by multiple tests)
[HarmonyPatch(typeof(OrderStackTargets), nameof(OrderStackTargets.Compute))]
public static class PrefixVH_A
{
    [HarmonyPrefix, HarmonyPriority(Priority.VeryHigh)]
    public static void Prefix(ref int x)
    {
        OrderStackProbes.Steps.Add("A");
        x = x * 10 + 1;
    }
}