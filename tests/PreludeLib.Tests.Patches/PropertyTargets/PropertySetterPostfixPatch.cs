using HarmonyLib;
using PreludeLib.Tests.Examples;

namespace PreludeLib.Tests.Patches.PropertyTargets;

/// 17) PatchPropertySetterWithMethodTypeSetter
[HarmonyPatch(typeof(PropertyPatchTargets), nameof(PropertyPatchTargets.P), methodType: MethodType.Setter)]
public static class PropertySetterPostfixPatch
{
    [HarmonyPostfix]
    public static void PSetterPostfix(PropertyPatchTargets __instance, int value)
    {
        // Prove the setter patch executed, without causing recursion
        __instance.Bump();
        // We do NOT reassign the property here to avoid recursive setter calls.
    }
}