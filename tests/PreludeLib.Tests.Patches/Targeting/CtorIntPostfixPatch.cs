using HarmonyLib;
using PreludeLib.Tests.Examples;

namespace PreludeLib.Tests.Patches.Targeting;

/// 14) PatchConstructorWithMethodTypeConstructor (specific overload: .ctor(int))
/// Use MethodType.Constructor and explicit argument types
[HarmonyPatch(typeof(TargetingExamples), methodType: MethodType.Constructor, argumentTypes: [typeof(int)])]
public static class CtorIntPostfixPatch
{
    [HarmonyPostfix]
    public static void CtorIntPostfix(TargetingExamples __instance, int baseVal)
    {
        TargetingProbes.CtorIntPostfixHit = true;
        TargetingProbes.CtorSeenBaseVal = baseVal;
        // We don't alter __instance here; just recording for test assertions.
    }
}