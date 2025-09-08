using HarmonyLib;
using PreludeLib.Tests.Examples;

namespace PreludeLib.Tests.Patches.Unpatch;

/// 31) continued
[HarmonyPatch(typeof(UnpatchTargets), nameof(UnpatchTargets.Compute))]
public static class OwnerB_PrefixPatch
{
    [HarmonyPrefix, HarmonyPriority(Priority.Low)]
    public static void Prefix(ref int x)
    {
        UnpatchProbes.Steps.Add("B");
        x = x * 10 + 2;
    }
}