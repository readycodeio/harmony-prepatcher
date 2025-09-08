using HarmonyLib;
using PreludeLib.Tests.Examples;

namespace PreludeLib.Tests.Patches.SpecialInjection;

/// 29) HarmonyArgumentAttributeBindsByIndexAndName
[HarmonyPatch(typeof(SpecialInjectionTargets), nameof(SpecialInjectionTargets.Combine))]
public static class HarmonyArgumentBindingPrefixPatch
{
    [HarmonyPrefix]
    public static void Prefix([HarmonyArgument(0)] ref int left,
        [HarmonyArgument("right")] ref int right)
    {
        // Mutate both arguments using index and name binding
        left += 2;
        right += 3;
    }
}