using HarmonyLib;
using PreludeLib.Tests.Examples;

namespace PreludeLib.Tests.Patches.Unpatch;

/// 30) continued
/// 
[HarmonyPatch(typeof(UnpatchTargets), nameof(UnpatchTargets.Compute))]
public static class UnpatchPrefixC
{
    [HarmonyPrefix, HarmonyPriority(Priority.Low)]
    public static void Prefix(ref int x)
    {
        UnpatchProbes.Steps.Add("C");
        x = x * 10 + 3;
    }
}