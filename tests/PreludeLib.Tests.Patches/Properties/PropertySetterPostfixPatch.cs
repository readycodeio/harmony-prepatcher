using HarmonyLib;
using PreludeLib.Tests.Examples;

namespace PreludeLib.Tests.Patches.Properties;

/// 17) PatchPropertySetterWithMethodTypeSetter
[HarmonyPatch(typeof(PropertyTargets), nameof(PropertyTargets.P), methodType: MethodType.Setter)]
public static class PropertySetterPostfixPatch
{
    [HarmonyPostfix]
    public static void PSetterPostfix(PropertyTargets __instance, int value)
    {
        // Prove the setter patch executed, without causing recursion
        __instance.Bump();
        // We do NOT reassign the property here to avoid recursive setter calls.
    }
}