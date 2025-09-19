using HarmonyLib;
using PreludeLib.Tests.Examples;

namespace PreludeLib.Tests.Patches.Categories;

/// Category: alpha
[HarmonyPatch(typeof(CategoryTargets), nameof(CategoryTargets.Op))]
[HarmonyPatchCategory("alpha")]
public static class AlphaCategoryPostfix
{
    [HarmonyPostfix]
    public static void Postfix(int x, ref int __result)
    {
        // Tag alpha by adding +100
        __result += 100;
    }
}