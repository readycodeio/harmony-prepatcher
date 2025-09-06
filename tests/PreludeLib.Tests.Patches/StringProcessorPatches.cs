using HarmonyLib;
using Microsoft.Extensions.Logging;
using PreludeLib.Tests.Utils;

namespace PreludeLib.Tests.Patches;

[HarmonyPatch(typeof(PreludeLib.Tests.Examples.StringProcessor))]
public class StringProcessorPatches
{
    [HarmonyPatch(nameof(PreludeLib.Tests.Examples.StringProcessor.ProcessString))]
    [HarmonyPrefix]
    public static void ProcessStringPrefix(string? input)
    {
        TestLoggerProvider.Logger.LogInformation("[PREFIX] Processing string: '{input}'", input ?? "<null>");
    }

    [HarmonyPatch(nameof(PreludeLib.Tests.Examples.StringProcessor.ProcessString))]
    [HarmonyPostfix]
    public static void ProcessStringPostfix(string input, ref string __result)
    {
        TestLoggerProvider.Logger.LogInformation("[POSTFIX] String processing result: '{result}'", __result);
        
        if (!string.IsNullOrEmpty(__result))
        {
            __result = "[PROCESSED] " + __result;
            TestLoggerProvider.Logger.LogInformation("[POSTFIX] Modified result: '{result}'", __result);
        }
    }

    [HarmonyPatch(nameof(PreludeLib.Tests.Examples.StringProcessor.ConcatenateStrings))]
    [HarmonyPrefix]
    public static bool ConcatenateStringsPrefix(
        PreludeLib.Tests.Examples.StringProcessor __instance, 
        string? first, 
        string? second, 
        ref string __result)
    {
        TestLoggerProvider.Logger.LogInformation("[PREFIX] Concatenating: '{first}' + '{second}'", first ?? "<null>", second ?? "<null>");
        
        if (string.IsNullOrEmpty(first) && string.IsNullOrEmpty(second))
        {
            TestLoggerProvider.Logger.LogInformation("[PREFIX] Both strings empty, returning special message");
            __result = "[EMPTY CONCATENATION]";
            return false;
        }
        
        return true;
    }

    [HarmonyPatch(nameof(PreludeLib.Tests.Examples.StringProcessor.ConcatenateStrings))]
    [HarmonyPostfix]
    public static void ConcatenateStringsPostfix(string first, string second, string __result)
    {
        TestLoggerProvider.Logger.LogInformation("[POSTFIX] Concatenation result: '{result}'", __result);
    }
}
