 using System.Reflection;
using HarmonyLib;
using PreludeLib.Tests.Examples;

namespace PreludeLib.Tests.Patches.SpecialInjection;

/// 28) Injected__originalMethodProvidesMethodBase (postfix to observe)
[HarmonyPatch(typeof(SpecialInjectionTargets), nameof(SpecialInjectionTargets.Combine))]
public static class OriginalMethodInjectionPostfixPatch
{
    [HarmonyPostfix]
    public static void Postfix(MethodBase __originalMethod)
    {
        SpecialInjectionProbes.LastOriginal = __originalMethod;
    }
}