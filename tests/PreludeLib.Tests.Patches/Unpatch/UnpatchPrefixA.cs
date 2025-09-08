using HarmonyLib;
using PreludeLib.Tests.Examples;

namespace PreludeLib.Tests.Patches.Unpatch;

/// 30) Three prefixes (A, B, C) then unpatch B only
[HarmonyPatch(typeof(UnpatchTargets), nameof(UnpatchTargets.Compute))]
public static class UnpatchPrefixA
{
    [HarmonyPrefix, HarmonyPriority(Priority.VeryHigh)]
    public static void Prefix(ref int x)
    {
        UnpatchProbes.Steps.Add("A");
        x = x * 10 + 1;
    }
}