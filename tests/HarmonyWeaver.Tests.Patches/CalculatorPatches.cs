using HarmonyLib;
using HarmonyWeaver.Core.Logging;
using System;

namespace HarmonyWeaver.Tests.Patches
{
    /// <summary>
    /// Example patches for the Calculator class
    /// </summary>
    [HarmonyPatch(typeof(HarmonyWeaver.Examples.Calculator))]
    public class CalculatorPatches
    {
        /// <summary>
        /// Prefix for Add method - logs the operation and can skip execution
        /// </summary>
        [HarmonyPatch(nameof(HarmonyWeaver.Examples.Calculator.Add))]
        [HarmonyPrefix]
        public static bool AddPrefix(int a, int b)
        {
            LoggerProvider.Logger.LogInfo($"[PREFIX] About to add {a} + {b}");
            
            // Return true to continue with original method, false to skip
            return true;
        }

        /// <summary>
        /// Postfix for Add method - logs the result
        /// </summary>
        [HarmonyPatch(nameof(HarmonyWeaver.Examples.Calculator.Add))]
        [HarmonyPostfix]
        public static void AddPostfix(int a, int b, int __result)
        {
            LoggerProvider.Logger.LogInfo($"[POSTFIX] Addition result: {a} + {b} = {__result}");
        }

        /// <summary>
        /// Prefix for Divide method - adds safety checks
        /// </summary>
        [HarmonyPatch(nameof(HarmonyWeaver.Examples.Calculator.Divide))]
        [HarmonyPrefix]
        public static bool DividePrefix(int a, int b, ref int __result)
        {
            LoggerProvider.Logger.LogInfo($"[PREFIX] About to divide {a} / {b}");
            
            if (b == 0)
            {
                LoggerProvider.Logger.LogWarning("[PREFIX] Division by zero detected, returning 0 instead of throwing");
                __result = 0;
                return false; // Skip original method
            }
            
            return true; // Continue with original method
        }

        /// <summary>
        /// Finalizer for Divide method - handles exceptions
        /// </summary>
        [HarmonyPatch(nameof(HarmonyWeaver.Examples.Calculator.Divide))]
        [HarmonyFinalizer]
        public static void DivideFinalizer(int a, int b, Exception __exception)
        {
            if (__exception != null)
            {
                LoggerProvider.Logger.LogError($"[FINALIZER] Exception caught in Divide({a}, {b}): {__exception.Message}");
            }
            else
            {
                LoggerProvider.Logger.LogInfo($"[FINALIZER] Divide({a}, {b}) completed successfully");
            }
        }
    }
}