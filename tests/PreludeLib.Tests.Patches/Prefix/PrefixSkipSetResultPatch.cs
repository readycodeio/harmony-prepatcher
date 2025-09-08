using HarmonyLib;
using PreludeLib.Tests.Examples;

namespace PreludeLib.Tests.Patches.Prefix;

/// 1) PrefixReturningFalseSkipsOriginalAndSetsResult
[HarmonyPatch(typeof(PrefixTargets))]
public static class PrefixSkipSetResultPatch
{
    [HarmonyPatch(nameof(PrefixTargets.Sum))]
    [HarmonyPrefix]
    public static bool SumPrefix_SetResultAndSkip(int a, int b, ref int __result)
    {
        // Prove skip by setting a sentinel that original would never return for (2,3)
        __result = -1;
        return false; // skip original
    }
}