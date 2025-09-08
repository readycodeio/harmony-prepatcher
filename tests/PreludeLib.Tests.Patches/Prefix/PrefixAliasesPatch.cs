using HarmonyLib;
using PreludeLib.Tests.Examples;

namespace PreludeLib.Tests.Patches.Prefix;

/// 4) PrefixCanUseArgumentIndexAliases__0__1
[HarmonyPatch(typeof(PrefixTargets))]
public static class PrefixAliasesPatch
{
    [HarmonyPatch(nameof(PrefixTargets.Sum))]
    [HarmonyPrefix]
    public static void SumPrefix_UseAliases(ref int __0, int __1)
    {
        // __0 is 'a' by alias; mutate it. __1 is 'b'; read-only here.
        __0 += 7;
    }
}