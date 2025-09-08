using HarmonyLib;
using PreludeLib.Tests.Examples;

namespace PreludeLib.Tests.Patches.Category;

/// Category: beta
[HarmonyPatch(typeof(CategoryTargets), nameof(CategoryTargets.Op))]
[HarmonyPatchCategory("beta")]
public static class BetaCategoryPostfix
{
    [HarmonyPostfix]
    public static void Postfix(int x, ref int __result)
    {
        // Tag beta by adding +1000
        __result += 1000;
    }
}