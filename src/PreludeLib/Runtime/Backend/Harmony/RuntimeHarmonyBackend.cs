using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using HarmonyLib;
using Microsoft.Extensions.Logging;
using PreludeLib.Runtime.Registry;

namespace PreludeLib.Runtime.Backend.HarmonyDetour;

public class RuntimeHarmonyBackend(ILogger logger) : RuntimePerIdPatchBackendBase(logger)
{
    private readonly Dictionary<string, Harmony> _harmonyInstances = [];

    protected override void DoPatch(
        MethodBase original,
        List<HarmonyMethod> prefixes, 
        List<HarmonyMethod> postfixes,
        List<HarmonyMethod> finalizers,
        List<HarmonyMethod> addedPrefixes,
        List<HarmonyMethod> addedPostfixes,
        List<HarmonyMethod> addedFinalizers,
        List<HarmonyMethod> removedPrefixes,
        List<HarmonyMethod> removedPostfixes,
        List<HarmonyMethod> removedFinalizers,
        string id
    )
    {
        var harmony = GetHarmonyInstance(id);
        
        foreach (var patchMethod in removedPrefixes)
        {
            logger.LogInformation("Unpatching {Original} prefix {Prefix}", original, patchMethod.method);
            harmony.Unpatch(original, patchMethod.method);
        }

        foreach (var patchMethod in removedPostfixes)
        {
            logger.LogInformation("Unpatching {Original} postfix {Postfix}", original, patchMethod.method);
            harmony.Unpatch(original, patchMethod.method);
        }

        foreach (var patchMethod in removedFinalizers)
        {
            logger.LogInformation("Unpatching {Original} finalizer {Finalizer}", original, patchMethod.method);
            harmony.Unpatch(original, patchMethod.method);
        }
        
        foreach (var patchMethod in addedPrefixes)
        {
            Logger.LogInformation("Patching {Original} prefix {Prefix}", original, patchMethod.method);
            harmony.Patch(original, prefix: patchMethod);
        }

        foreach (var patchMethod in addedPostfixes)
        {
            Logger.LogInformation("Patching {Original} postfix {Postfix}", original, patchMethod.method);
            harmony.Patch(original, postfix: patchMethod); 
        }

        foreach (var patchMethod in addedFinalizers)
        {
            Logger.LogInformation("Patching {Original} finalizer {Finalizer}", original, patchMethod.method);
            harmony.Patch(original, finalizer: patchMethod);
        }
    }

    protected override Harmony GetHarmonyInstance(string id)
    {
        if (!_harmonyInstances.TryGetValue(id, out var harmony))
        {
            harmony = new Harmony(id);
            _harmonyInstances[id] = harmony;
        }

        return harmony;
    }

    public override void Commit(IRuntimePatchRegistry registry)
    {
        PatchFunctions.LoggerFunc = Logger.LogDebug;
        try
        {
            base.Commit(registry);
        }
        finally
        {
            PatchFunctions.LoggerFunc = null;
        }
    }
    
    private void PrepareGroupBlock(IRuntimePatchRegistry registry, PatchGroup group, string id, Action action)
    {
        if (PrepareGroup(registry, out var exception, group, id))
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        }
        
        CleanupGroup(registry, exception, group, id);
    }
    
    public bool PrepareGroup(IRuntimePatchRegistry registry, out Exception? exception, PatchGroup group, string id)
    {
        var harmony = _harmonyInstances[id];
        var context = new RuntimeAuxiliaryMethodContext(harmony, group.ContainerType, null, null!, Logger);
        try
        {
            exception = null;
            var callback = registry.GetPrepareGroupCallback(group, id);
            return RunMethod<bool>(callback, true, false, context, parameters: [harmony]);
        }
        catch (Exception ex)
        {
            exception = ex;
            return false;
        }
    }

    public void CleanupGroup(IRuntimePatchRegistry registry, Exception? exception, PatchGroup group, string id)
    {
        var harmony = _harmonyInstances[id];
        var context = new RuntimeAuxiliaryMethodContext(harmony, group.ContainerType, null, null!, Logger);
        try
        {
            var callback = registry.GetCleanupGroupCallback(group, id);
            RunMethod(callback, ref exception, context, parameters: [exception, harmony]);
        }
        catch (Exception ex)
        {
            exception ??= ex;
        }
        finally
        {
            ReportException(exception, context);
        }
    }

    private void PrepareCleanupPatchMethodBlock(IRuntimePatchRegistry registry, PatchGroup group, MethodBase original, string id, HarmonyMethod patchMethod, Action action)
    {
        if (PreparePatchMethod(registry, out var exception, group, original, id, patchMethod))
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        }
                        
        CleanupPatchMethod(registry, exception, group, original, id, patchMethod);
    }
    
    public bool PreparePatchMethod(IRuntimePatchRegistry registry, out Exception? exception, PatchGroup group, MethodBase original, string id, HarmonyMethod patchMethod)
    {
        var harmony = _harmonyInstances[id];
        var context = new RuntimeAuxiliaryMethodContext(harmony, group.ContainerType, original, patchMethod.method, Logger);
        try
        {
            exception = null;
            var callback = registry.GetCleanupPatchMethodCallback(patchMethod, id);
            return RunMethod<bool>(callback, true, false, context, parameters: [harmony, original]);
        }
        catch (Exception ex)
        {
            exception = ex;
            return false;
        }
    }
    
    public void CleanupPatchMethod(IRuntimePatchRegistry registry, Exception? exception, PatchGroup group, MethodBase original, string id, HarmonyMethod patchMethod)
    {
        var harmony = _harmonyInstances[id];
        var context = new RuntimeAuxiliaryMethodContext(harmony, group.ContainerType, original, patchMethod.method, Logger);
        try
        {
            var callback = registry.GetCleanupPatchMethodCallback(patchMethod, id);
            RunMethod(callback, ref exception, context, parameters: [original, exception, harmony]);
        }
        catch (Exception ex)
        {
            exception ??= ex;
        }
        finally
        {
            ReportException(exception, context);
        }
    }
    
    [SuppressMessage("Style", "IDE0300")]
    private T RunMethod<T>(MethodInfo? callback, T defaultIfNotExisting, T defaultIfFailing, RuntimeAuxiliaryMethodContext context, Func<T, string>? failOnResult = null, params object[]? parameters)
    {
        if (callback != null)
        {
            var actualParameters = AccessTools.ActualParameters(callback, parameters);

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

        return defaultIfNotExisting;
    }

    [SuppressMessage("Style", "IDE0300")]
    private void RunMethod(MethodInfo? callback, ref Exception? exception, RuntimeAuxiliaryMethodContext context, params object?[]? parameters)
    {
        if (callback != null)
        {
            var actualParameters = AccessTools.ActualParameters(callback, parameters);
            try
            {
                var result = callback.Invoke(null, actualParameters);
                if (callback.ReturnType == typeof(Exception))
                    exception = result as Exception;
            }
            catch (Exception ex)
            {
                ReportException(ex, context);
            }
        }
    }
    
    private void ReportException(Exception? exception, RuntimeAuxiliaryMethodContext context)
    {
        if (exception is null)
            return;
        _ = Harmony.VersionInfo(out var currentVersion);

        Logger.LogDebug($"### Exception from user \"{context.HarmonyInstance.Id}\", Harmony v{currentVersion}");
        Logger.LogDebug($"### Original: {(context.Original?.FullDescription() ?? "NULL")}");
        Logger.LogDebug($"### Patch class: {context.ContainerType.FullDescription()}");
        var logException = exception;
        if (logException is HarmonyException hEx)
            logException = hEx.InnerException!;
        var exStr = logException.ToString();
        while (exStr.Contains("\n\n"))
            exStr = exStr.Replace("\n\n", "\n");
        exStr = exStr.Split('\n').Join(line => $"### {line}", "\n");
        Logger.LogDebug(exStr.Trim());

        if (exception is HarmonyException)
            throw exception; // assume HarmonyException already wraps the actual exception
        throw new HarmonyException($"Patching exception in method {context.Original.FullDescription()}", exception);
    }
}