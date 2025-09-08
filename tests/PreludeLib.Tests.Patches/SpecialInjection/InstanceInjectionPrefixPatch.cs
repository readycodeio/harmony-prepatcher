using HarmonyLib;
using PreludeLib.Tests.Examples;

namespace PreludeLib.Tests.Patches.SpecialInjection;

/// 26) Injected__instanceProvidesOriginalInstance
[HarmonyPatch(typeof(SpecialInjectionTargets), nameof(SpecialInjectionTargets.SumWithOffset))]
public static class InstanceInjectionPrefixPatch
{
    [HarmonyPrefix]
    public static void Prefix(SpecialInjectionTargets __instance)
    {
        // Record the instance and modify its state to prove we have the *real* object
        SpecialInjectionProbes.LastInstance = __instance;
        __instance.SetOffset(10);
    }
}