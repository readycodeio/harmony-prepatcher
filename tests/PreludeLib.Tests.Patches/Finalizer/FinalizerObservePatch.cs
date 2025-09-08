using HarmonyLib;
using PreludeLib.Tests.Examples;

namespace PreludeLib.Tests.Patches.Finalizer;

/// 9 & 11) Observe-only finalizer: sees __exception (may be null) but does NOT suppress it
[HarmonyPatch(typeof(FinalizerTargets))]
public static class FinalizerObservePatch
{
    [HarmonyPatch(nameof(FinalizerTargets.MightThrow))]
    [HarmonyFinalizer]
    public static void MightThrowFinalizer_Observe(int x, Exception? __exception)
    {
        FinalizerProbes.FinalizerRan = true;
        FinalizerProbes.LastException = __exception;
        // void finalizer => does not alter the exception flow
    }
}