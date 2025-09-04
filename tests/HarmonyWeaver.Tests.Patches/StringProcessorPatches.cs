using HarmonyLib;
using HarmonyWeaver.Core.Logging;
using System;

namespace HarmonyWeaver.Tests.Patches
{
    /// <summary>
    /// Example patches for the StringProcessor class
    /// </summary>
    [HarmonyPatch(typeof(HarmonyWeaver.Examples.StringProcessor))]
    public class StringProcessorPatches
    {
        /// <summary>
        /// Prefix for ProcessString method - logs input
        /// </summary>
        [HarmonyPatch(nameof(HarmonyWeaver.Examples.StringProcessor.ProcessString))]
        [HarmonyPrefix]
        public static void ProcessStringPrefix(string input)
        {
            LoggerProvider.Logger.LogInfo($"[PREFIX] Processing string: '{input ?? "<null>"}'");
        }

        /// <summary>
        /// Postfix for ProcessString method - logs result and can modify it
        /// </summary>
        [HarmonyPatch(nameof(HarmonyWeaver.Examples.StringProcessor.ProcessString))]
        [HarmonyPostfix]
        public static void ProcessStringPostfix(string input, ref string __result)
        {
            LoggerProvider.Logger.LogInfo($"[POSTFIX] String processing result: '{__result}'");
            
            // Example modification: add a prefix to all processed strings
            if (!string.IsNullOrEmpty(__result))
            {
                __result = "[PROCESSED] " + __result;
                LoggerProvider.Logger.LogInfo($"[POSTFIX] Modified result: '{__result}'");
            }
        }

        /// <summary>
        /// Prefix for ConcatenateStrings method with __instance parameter
        /// </summary>
        [HarmonyPatch(nameof(HarmonyWeaver.Examples.StringProcessor.ConcatenateStrings))]
        [HarmonyPrefix]
        public static bool ConcatenateStringsPrefix(
            HarmonyWeaver.Examples.StringProcessor __instance, 
            string first, 
            string second, 
            ref string __result)
        {
            LoggerProvider.Logger.LogInfo($"[PREFIX] Concatenating: '{first ?? "<null>"}' + '{second ?? "<null>"}'");
            
            // Example: if both strings are null or empty, return a special message
            if (string.IsNullOrEmpty(first) && string.IsNullOrEmpty(second))
            {
                LoggerProvider.Logger.LogInfo("[PREFIX] Both strings empty, returning special message");
                __result = "[EMPTY CONCATENATION]";
                return false; // Skip original method
            }
            
            return true; // Continue with original method
        }

        /// <summary>
        /// Postfix for ConcatenateStrings method
        /// </summary>
        [HarmonyPatch(nameof(HarmonyWeaver.Examples.StringProcessor.ConcatenateStrings))]
        [HarmonyPostfix]
        public static void ConcatenateStringsPostfix(string first, string second, string __result)
        {
            LoggerProvider.Logger.LogInfo($"[POSTFIX] Concatenation result: '{__result}'");
        }
    }
}