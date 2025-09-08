using HarmonyLib;
using PreludeLib.Tests.Examples;

namespace PreludeLib.Tests.Patches.PrivateField;

/// 24 & 25) Prefix modifies private field via ref ___secret; Postfix observes final value
[HarmonyPatch(typeof(PrivateFieldTargets), nameof(PrivateFieldTargets.Bump))]
public static class PrivateFieldModifyAndObservePatch
{
    [HarmonyPrefix]
    public static void BumpPrefix_Modify(ref int ___secret)
    {
        // Change the private field before original runs
        ___secret += 10;
        PrivateFieldProbes.PrefixSeenSecret = ___secret; // record mutated field seen by prefix
    }

    [HarmonyPostfix]
    public static void BumpPostfix_Observe(int ___secret)
    {
        // Observe the (potentially) mutated field after original runs
        PrivateFieldProbes.PostfixSeenSecret = ___secret;
    }
}