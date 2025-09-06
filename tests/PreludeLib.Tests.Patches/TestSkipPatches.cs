using HarmonyLib;
using Microsoft.Extensions.Logging;
using PreludeLib.Tests.Utils;

namespace PreludeLib.Tests.Patches;

[HarmonyPatch(typeof(PreludeLib.Tests.Examples.Calculator))]
public class TestSkipPatches
{
    [HarmonyPatch(nameof(PreludeLib.Tests.Examples.Calculator.Multiply))]
    [HarmonyPrefix]
    public static bool MultiplySkipPrefix(int a, int b, ref int __result)
    {
        TestLoggerProvider.Logger.LogInformation("[SKIP PREFIX] Multiply({a}, {b}) - returning custom result", a, b);
        
        __result = 999;
        return false;
    }

    [HarmonyPatch(nameof(PreludeLib.Tests.Examples.Calculator.Subtract))]
    [HarmonyPrefix]
    public static bool SubtractConditionalPrefix(int a, int b, ref int __result)
    {
        TestLoggerProvider.Logger.LogInformation("[CONDITIONAL PREFIX] Subtract({a}, {b})", a, b);
        
        if (a == 100 && b == 1)
        {
            TestLoggerProvider.Logger.LogInformation("[CONDITIONAL PREFIX] Special case detected, returning 42");
            __result = 42;
            return false;
        }
        
        TestLoggerProvider.Logger.LogInformation("[CONDITIONAL PREFIX] Normal case, continuing with original method");
        return true;
    }
}