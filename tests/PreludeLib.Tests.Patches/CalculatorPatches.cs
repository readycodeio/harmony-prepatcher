using System;
using HarmonyLib;
using Microsoft.Extensions.Logging;
using PreludeLib.Tests.Utils;

namespace PreludeLib.Tests.Patches;

[HarmonyPatch(typeof(PreludeLib.Tests.Examples.Calculator))]
public class CalculatorPatches
{
    [HarmonyPatch(nameof(PreludeLib.Tests.Examples.Calculator.Add))]
    [HarmonyPrefix]
    public static bool AddPrefix(int a, int b)
    {
        TestLoggerProvider.Logger.LogInformation("[PREFIX] About to add {a} + {b}", a, b);
        
        return true;
    }

    [HarmonyPatch(nameof(PreludeLib.Tests.Examples.Calculator.Add))]
    [HarmonyPostfix]
    public static void AddPostfix(int a, int b, int __result)
    {
        TestLoggerProvider.Logger.LogInformation("[POSTFIX] Addition result: {a} + {b} = {result}", a, b, __result);
    }

    [HarmonyPatch(nameof(PreludeLib.Tests.Examples.Calculator.Divide))]
    [HarmonyPrefix]
    public static bool DividePrefix(int a, int b, ref int __result)
    {
        TestLoggerProvider.Logger.LogInformation("[PREFIX] About to divide {a} / {b}", a, b);
        
        if (b == 0)
        {
            TestLoggerProvider.Logger.LogWarning("[PREFIX] Division by zero detected, returning 0 instead of throwing");
            __result = 0;
            return false;
        }
        
        return true;
    }

    [HarmonyPatch(nameof(PreludeLib.Tests.Examples.Calculator.Divide))]
    [HarmonyFinalizer]
    public static void DivideFinalizer(int a, int b, Exception? __exception)
    {
        if (__exception != null)
        {
            TestLoggerProvider.Logger.LogError("[FINALIZER] Exception caught in Divide({a}, {b}): {message}", a, b, __exception.Message);
        }
        else
        {
            TestLoggerProvider.Logger.LogInformation("[FINALIZER] Divide({a}, {b}) completed successfully", a, b);
        }
    }
}
