using HarmonyLib;
using PreludeLib.Tests.Examples;

namespace PreludeLib.Tests.Patches.Ordering;

/// 35) & 38) continued; Y must run AFTER Z
[HarmonyPatch(typeof(OrderStackTargets), nameof(OrderStackTargets.Compute))]
public static class OwnerY_Prefix
{
    [HarmonyPrefix, HarmonyPriority(Priority.Normal)]
    public static void Prefix(ref int x)
    {
        OrderStackProbes.Steps.Add("Y");
        x = x * 10 + 2;
    }
}