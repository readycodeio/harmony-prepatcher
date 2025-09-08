using HarmonyLib;
using PreludeLib.Tests.Examples;

namespace PreludeLib.Tests.Patches.SpecialInjection;

/// 27) Injected__argsArrayCanMutateArgumentsInPlace
[HarmonyPatch(typeof(SpecialInjectionTargets), nameof(SpecialInjectionTargets.Add))]
public static class ArgsArrayInjectionPrefixPatch
{
    [HarmonyPrefix]
    public static void Prefix(object[] __args)
    {
        // __args are mutable; change both arguments in-place
        __args[0] = (int)__args[0] + 1; // a += 1
        __args[1] = (int)__args[1] + 2; // b += 2
        SpecialInjectionProbes.LastArgsSnapshot = new[] { (int)__args[0], (int)__args[1] };
    }
}