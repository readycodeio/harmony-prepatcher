using HarmonyLib;
using PreludeLib.Tests.Examples;

namespace PreludeLib.Tests.Patches.Targeting;

/// 12) PatchByMethodNameAndSignatureTargetsCorrectOverload
[HarmonyPatch(typeof(TargetingExamples))]
public static class Overload2PostfixPatch
{
    [HarmonyPatch(nameof(TargetingExamples.Over), typeof(int), typeof(int))]
    [HarmonyPostfix]
    public static void Over_IntInt_Postfix(int x, int y, ref int __result)
    {
        // Prove we hit ONLY this overload by changing result
        __result += 1000;
        TargetingProbes.Over2PostfixHit = true;
    }
}