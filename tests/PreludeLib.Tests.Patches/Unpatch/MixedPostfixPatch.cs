using System.Reflection;
using HarmonyLib;
using PreludeLib.Tests.Examples;

namespace PreludeLib.Tests.Patches.Unpatch;

/// 32) continued
[HarmonyPatch(typeof(UnpatchTargets), nameof(UnpatchTargets.Compute))]
public static class MixedPostfixPatch
{
    [HarmonyPostfix]
    public static void Postfix(ref int __result)
    {
        UnpatchProbes.Steps.Add("Post");
        __result += 100;
    }

    // Expose MethodInfo of this postfix for precise Unpatch()
    public static MethodInfo MethodInfo() =>
        typeof(MixedPostfixPatch).GetMethod(nameof(Postfix), BindingFlags.Public | BindingFlags.Static)!;
}