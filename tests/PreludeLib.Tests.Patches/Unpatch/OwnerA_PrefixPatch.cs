using HarmonyLib;
using PreludeLib.Tests.Examples;

namespace PreludeLib.Tests.Patches.Unpatch;

/// 31) continued
[HarmonyPatch(typeof(UnpatchTargets), nameof(UnpatchTargets.Compute))]
public static class OwnerA_PrefixPatch
{
    [HarmonyPrefix, HarmonyPriority(Priority.VeryHigh)]
    public static void Prefix(ref int x)
    {
        UnpatchProbes.Steps.Add("A");
        x = x * 10 + 1;
    }
}