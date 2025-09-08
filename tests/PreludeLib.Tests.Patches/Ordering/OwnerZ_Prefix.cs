 using HarmonyLib;
using PreludeLib.Tests.Examples;

namespace PreludeLib.Tests.Patches.Ordering;

/// 54) & 38) continued; Z has no constraints; will be placed first by the chain
[HarmonyPatch(typeof(OrderStackTargets), nameof(OrderStackTargets.Compute))]
public static class OwnerZ_Prefix
{
    [HarmonyPrefix, HarmonyPriority(Priority.VeryHigh)]
    public static void Prefix(ref int x)
    {
        OrderStackProbes.Steps.Add("Z");
        x = x * 10 + 3;
    }
}