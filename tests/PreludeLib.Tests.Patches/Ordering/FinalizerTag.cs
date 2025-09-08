using HarmonyLib;
using PreludeLib.Tests.Examples;

namespace PreludeLib.Tests.Patches.Ordering;

/// 37) Finalizer to prove it runs after postfix
[HarmonyPatch(typeof(OrderStackTargets), nameof(OrderStackTargets.Compute))]
public static class FinalizerTag
{
    [HarmonyFinalizer]
    public static void Finalizer()
    {
        OrderStackProbes.Steps.Add("F");
    }
}