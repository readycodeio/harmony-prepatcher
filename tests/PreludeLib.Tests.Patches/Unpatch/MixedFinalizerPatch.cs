using HarmonyLib;
using PreludeLib.Tests.Examples;

namespace PreludeLib.Tests.Patches.Unpatch;

/// 32) prefix + postfix + finalizer; remove only postfix
[HarmonyPatch(typeof(UnpatchTargets), nameof(UnpatchTargets.Compute))]
public static class MixedFinalizerPatch
{
    [HarmonyFinalizer]
    public static void Finalizer(Exception? __exception)
    {
        UnpatchProbes.Steps.Add("Fin");
        // No suppression/changes needed; just prove it ran
    }
}