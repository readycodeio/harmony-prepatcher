using HarmonyLib;
using PreludeLib.Tests.Examples;

namespace PreludeLib.Tests.Patches.Categories;

/// Uncategorized
[HarmonyPatch(typeof(CategoryTargets), nameof(CategoryTargets.Op))]
public static class UncategorizedPostfix
{
    [HarmonyPostfix]
    public static void Postfix(int x, ref int __result)
    {
        // Tag uncategorized by adding +10
        __result += 10;
    }
}