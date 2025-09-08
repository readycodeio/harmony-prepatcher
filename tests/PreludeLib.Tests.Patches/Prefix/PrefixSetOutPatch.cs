using HarmonyLib;
using PreludeLib.Tests.Examples;

namespace PreludeLib.Tests.Patches.Prefix;

/// 3) PrefixCanSetOutParameterValues
[HarmonyPatch(typeof(PrefixTargets))]
public static class PrefixSetOutPatch
{
    [HarmonyPatch(nameof(PrefixTargets.MakePair))]
    [HarmonyPrefix]
    public static bool MakePairPrefix(int seed, ref int a, ref int b)
    {
        // Assign out values (as ref) and skip original
        a = seed * 10;
        b = seed * 100;
        return false; // skip original
    }
}