using HarmonyLib;
using PreludeLib.Tests.Examples;

namespace PreludeLib.Tests.Patches.PropertyTargets;

/// 18) PrefixOnAutoPropertySetterCanModifyIncomingValue
[HarmonyPatch(typeof(PropertyPatchTargets), nameof(PropertyPatchTargets.Auto), methodType: MethodType.Setter)]
public static class AutoSetterPrefixPatch
{
    [HarmonyPrefix]
    public static void AutoSetterPrefix(ref int value)
    {
        // Mutate the incoming 'value' before original setter sees it
        value += 10;
    }
}