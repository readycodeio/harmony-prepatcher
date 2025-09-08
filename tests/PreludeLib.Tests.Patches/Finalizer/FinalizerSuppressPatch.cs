 using HarmonyLib;
using PreludeLib.Tests.Examples;

namespace PreludeLib.Tests.Patches.Finalizer;

/// 10) Suppressing finalizer: returns Exception and sets __result when original throws
[HarmonyPatch(typeof(FinalizerTargets))]
public static class FinalizerSuppressPatch
{
    [HarmonyPatch(nameof(FinalizerTargets.MightThrow))]
    [HarmonyFinalizer]
    public static Exception? MightThrowFinalizer_Suppress(int x, ref int __result, Exception? __exception)
    {
        FinalizerProbes.FinalizerRan = true;
        FinalizerProbes.LastException = __exception;

        if (__exception != null)
        {
            // Supply a fallback result and suppress the exception by returning null
            __result = -99;
            return null; // null => suppress original exception
        }

        return null; // nothing to change on success
    }
}