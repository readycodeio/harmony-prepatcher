using HarmonyLib;
using PreludeLib.Tests.Examples;

namespace PreludeLib.Tests.Patches.Ordering;

/// 35) & 38) continued; X must run AFTER Y
[HarmonyPatch(typeof(OrderStackTargets), nameof(OrderStackTargets.Compute))]
public static class OwnerX_Prefix
{
    [HarmonyPrefix, HarmonyPriority(Priority.Low)]
    public static void Prefix(ref int x)
    {
        OrderStackProbes.Steps.Add("X");
        x = x * 10 + 1;
    }
}