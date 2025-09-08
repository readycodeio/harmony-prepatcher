using HarmonyLib;
using PreludeLib.Tests.Examples;

namespace PreludeLib.Tests.Patches.PrivateField;

/// 23) PrefixCanReadPrivateFieldViaTripleUnderscore
[HarmonyPatch(typeof(PrivateFieldTargets), nameof(PrivateFieldTargets.Bump))]
public static class PrivateFieldReadPrefixPatch
{
    [HarmonyPrefix]
    public static void BumpPrefix_Read(int ___secret)
    {
        // Read-only capture of the private field
        PrivateFieldProbes.PrefixSeenSecret = ___secret;
    }
}