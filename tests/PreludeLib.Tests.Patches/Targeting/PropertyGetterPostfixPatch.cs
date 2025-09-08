using HarmonyLib;
using PreludeLib.Tests.Examples;

namespace PreludeLib.Tests.Patches.Targeting;

/// 13) PatchPropertyGetterWithMethodTypeGetter
/// Use attribute MethodType = MethodType.Getter, targeting the 'Value' property getter
[HarmonyPatch(typeof(TargetingExamples), nameof(TargetingExamples.Value), methodType: MethodType.Getter)]
public static class PropertyGetterPostfixPatch
{
    [HarmonyPostfix]
    public static void ValueGetterPostfix(ref int __result)
    {
        __result += 10; // adjust getter result to prove patching
        TargetingProbes.GetterPostfixHit = true;
    }
}