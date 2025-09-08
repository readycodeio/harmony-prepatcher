using HarmonyLib;
using PreludeLib.Tests.Examples;

namespace PreludeLib.Tests.Patches.Postfix;

/// 8) PostfixSeesArgsAfterPrefixModifications
[HarmonyPatch(typeof(PostfixTargets))]
public static class PostfixSeesArgsAfterPrefixPatch
{
    [HarmonyPatch(nameof(PostfixTargets.Combine))]
    [HarmonyPrefix]
    public static void CombinePrefix(ref int a, ref int b)
    {
        // Mutate incoming args before original
        a += 1;
        b += 2;
    }

    [HarmonyPatch(nameof(PostfixTargets.Combine))]
    [HarmonyPostfix]
    public static void CombinePostfix(int a, int b, ref int __result)
    {
        // Postfix should see mutated values (a+1, b+2); record them for assertions
        PostfixProbes.ObservedA = a;
        PostfixProbes.ObservedB = b;

        // __result should already reflect mutated args (a + b)
        // No change here; just demonstrating visibility.
    }
}