using HarmonyLib;
using PreludeLib.Tests.Examples;

namespace PreludeLib.Tests.Patches.Unpatch;

/// 32) continued
[HarmonyPatch(typeof(UnpatchTargets), nameof(UnpatchTargets.Compute))]
public static class MixedPrefixPatch
{
    [HarmonyPrefix]
    public static void Prefix(ref int x)
    {
        UnpatchProbes.Steps.Add("Pre");
        x = x * 10 + 1;
    }
}