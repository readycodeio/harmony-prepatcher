using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using HarmonyLib;
using Microsoft.Extensions.Logging;
using PreludeLib.Runtime.Registry;

namespace PreludeLib.Runtime.Backend;

internal static class RuntimeBackendUtils
{
    public static IEnumerable<MethodBase> GetTargetOriginals(PatchTarget target, RuntimeAuxiliaryMethodContext context)
    {
        if (target.OriginalMethod != null)
        {
            return [target.OriginalMethod];
        }
        else if (target.TargetMethod != null)
        {
            if (typeof(MethodBase).IsAssignableFrom(target.TargetMethod.ReturnType))
            {
                var result = RunMethod<MethodBase?>(target.TargetMethod, context, null, null,
                    m => m is null ? "null" : (string?)null);
                return result != null ? [result] : [];
            }
            else if (typeof(IEnumerable<MethodBase>).IsAssignableFrom(target.TargetMethod.ReturnType))
            {
                return RunMethod<IEnumerable<MethodBase>?>(target.TargetMethod, context, null, null) ?? [];
            }
            else
            {
                throw new Exception($"Target method {target.TargetMethod.FullDescription()} has wrong return type (should be MethodBase or IEnumerable<MethodBase>)");
            }
        }
        else
        {
            return [];
        }
    }
    
    [SuppressMessage("Style", "IDE0300")]
    private static T RunMethod<T>(MethodInfo callback, RuntimeAuxiliaryMethodContext context, T defaultIfNotExisting, T defaultIfFailing, Func<T, string?>? failOnResult = null, params object[]? parameters)
    {
        var input = (parameters ?? []).Union([context.HarmonyInstance]).ToArray();
        var actualParameters = AccessTools.ActualParameters(callback, input);

        if (callback.ReturnType != typeof(void) && typeof(T).IsAssignableFrom(callback.ReturnType) is false)
            throw new Exception($"Method {callback.FullDescription()} has wrong return type (should be assignable to {typeof(T).FullName})");

        var result = defaultIfFailing;
        try
        {
            if (callback.ReturnType == typeof(void))
            {
                _ = callback.Invoke(null, actualParameters);
                result = defaultIfNotExisting;
            }
            else
                result = (T)callback.Invoke(null, actualParameters)!;

            if (failOnResult is not null)
            {
                var error = failOnResult(result);
                if (error is not null)
                    throw new Exception($"Method {callback.FullDescription()} returned an unexpected result: {error}");
            }
        }
        catch (Exception ex)
        {
            ReportException(ex, context);
        }
        return result;
    }
    
    private static void ReportException(Exception? exception, RuntimeAuxiliaryMethodContext context)
    {
        if (exception is null)
            return;

        _ = Harmony.VersionInfo(out var currentVersion);

        context.Logger.LogDebug($"### Exception from user \"{context.HarmonyInstance.Id}\", Harmony v{currentVersion}");
        context.Logger.LogDebug($"### Original: {(context.Original?.FullDescription() ?? "NULL")}");
        context.Logger.LogDebug($"### Patch class: {context.ContainerType.FullDescription()}");
        var logException = exception;
        if (logException is HarmonyException hEx)
            logException = hEx.InnerException!;
        var exStr = logException.ToString();
        while (exStr.Contains("\n\n"))
            exStr = exStr.Replace("\n\n", "\n");
        exStr = exStr.Split('\n').Join(line => $"### {line}", "\n");
        context.Logger.LogDebug(exStr.Trim());

        if (exception is HarmonyException)
            throw exception; // assume HarmonyException already wraps the actual exception
        throw new HarmonyException($"Patching exception in method {context.Original.FullDescription()}", exception);
    }
}