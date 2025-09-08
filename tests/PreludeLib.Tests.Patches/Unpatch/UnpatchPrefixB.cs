using HarmonyLib;
using PreludeLib.Tests.Examples;

namespace PreludeLib.Tests.Patches.Unpatch;

/// 30) continued
[HarmonyPatch(typeof(UnpatchTargets), nameof(UnpatchTargets.Compute))]
public static class UnpatchPrefixB
{
    [HarmonyPrefix, HarmonyPriority(Priority.Normal)]
    public static void Prefix(ref int x)
    {
        UnpatchProbes.Steps.Add("B");
        x = x * 10 + 2;
    }
}