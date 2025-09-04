using HarmonyLib;
using System;

namespace HarmonyWeaver.Tests.Patches
{
    /// <summary>
    /// Patches specifically designed to test the prefix skip functionality
    /// </summary>
    [HarmonyPatch(typeof(HarmonyWeaver.Examples.Calculator))]
    public class TestSkipPatches
    {
        /// <summary>
        /// A prefix that always returns false to skip the original method
        /// </summary>
        [HarmonyPatch(nameof(HarmonyWeaver.Examples.Calculator.Multiply))]
        [HarmonyPrefix]
        public static bool MultiplySkipPrefix(int a, int b, ref int __result)
        {
            Console.WriteLine($"[SKIP PREFIX] Multiply({a}, {b}) - returning custom result");
            
            // Set a custom result and skip the original method
            __result = 999; // Custom result instead of a * b
            return false; // Skip the original method
        }

        /// <summary>
        /// A prefix that conditionally skips based on input
        /// </summary>
        [HarmonyPatch(nameof(HarmonyWeaver.Examples.Calculator.Subtract))]
        [HarmonyPrefix]
        public static bool SubtractConditionalPrefix(int a, int b, ref int __result)
        {
            Console.WriteLine($"[CONDITIONAL PREFIX] Subtract({a}, {b})");
            
            if (a == 100 && b == 1)
            {
                // Special case: return custom result
                __result = 42;
                return false; // Skip original method
            }
            
            return true; // Continue with original method
        }
    }
}