using HarmonyLib;
using PreludeLib.Tests.Examples;

namespace PreludeLib.Tests.Patches.Properties;

/// 18) PrefixOnAutoPropertySetterCanModifyIncomingValue
[HarmonyPatch(typeof(PropertyTargets), nameof(PropertyTargets.Auto), methodType: MethodType.Setter)]
public static class AutoSetterPrefixPatch
{
    [HarmonyPrefix]
    public static void AutoSetterPrefix(ref int value)
    {
        // Mutate the incoming 'value' before original setter sees it
        value += 10;
    }
}