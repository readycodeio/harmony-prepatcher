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

[HarmonyPatch(typeof(ForeachTargets), nameof(ForeachTargets.NestedExample))]
public class ForeachNestedPatch
{
    [HarmonyPrefix]
    public static void Prefix(ref int searchX, ref int searchY, ref int searchZ)
    {
        searchX++;
        searchY++;
        searchZ++;
    }

    [HarmonyPostfix]
    public static void Postfix(ref int __result)
    {
        if (__result == 333)
            __result = 555;
    }
    
    [HarmonyFinalizer]
    public static Exception? Finalizer(Exception? __exception)
    {
        if (__exception == null)
            return null;
        else if (__exception.Message == "abc")
            return new ArgumentException("xyz", __exception);
        else
            return __exception;
    }
}