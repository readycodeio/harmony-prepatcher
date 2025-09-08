using HarmonyLib;
using PreludeLib.Tests.Examples;

namespace PreludeLib.Tests.Patches.Ordering;

/// 33) & 34) continued
[HarmonyPatch(typeof(OrderStackTargets), nameof(OrderStackTargets.Compute))]
public static class PrefixHigh_C
{
    [HarmonyPrefix, HarmonyPriority(Priority.High)]
    public static void Prefix(ref int x)
    {
        OrderStackProbes.Steps.Add("C");
        x = x * 10 + 3;
    }
}