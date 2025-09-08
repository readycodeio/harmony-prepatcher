using HarmonyLib;
using PreludeLib.Tests.Examples;

namespace PreludeLib.Tests.Patches.Prefix;

/// 2) PrefixCanModifyByRefArguments
[HarmonyPatch(typeof(PrefixTargets))]
public static class PrefixModifyByRefPatch
{
    [HarmonyPatch(nameof(PrefixTargets.MultiplyRef))]
    [HarmonyPrefix]
    public static void MultiplyRefPrefix(ref int x, int factor)
    {
        // Mutate ref arg before original executes
        x += 2;
        // Returning void means "continue to original"
    }
}