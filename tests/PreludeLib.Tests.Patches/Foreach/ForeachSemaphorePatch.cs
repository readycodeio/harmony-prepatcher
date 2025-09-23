using HarmonyLib;
using PreludeLib.Tests.Examples;

namespace PreludeLib.Tests.Patches.Foreach;

[HarmonyPatch(typeof(ForeachTargets), nameof(ForeachTargets.Example))]
public class ForeachSemaphorePatch
{
    private static readonly SemaphoreSlim Semaphore = new(1, 1);

    [HarmonyPrefix]
    public static void Prefix()
    {
        Semaphore.Wait();
    }

    [HarmonyPostfix]
    public static void Postfix(ref int __result)
    {
        __result = 5;
        Semaphore.Release();
    }
}