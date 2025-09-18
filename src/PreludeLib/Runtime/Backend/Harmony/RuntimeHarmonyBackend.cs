using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using HarmonyLib;
using Microsoft.Extensions.Logging;
using PreludeLib.Runtime.Registry;

namespace PreludeLib.Runtime.Backend.HarmonyDetour;

public class RuntimeHarmonyBackend(ILogger logger) : IRuntimeBackend
{
    private readonly Dictionary<string, Harmony> _harmonyInstances = [];

    private struct MethodCallContext(Harmony harmony, MethodBase? original, MethodInfo patchMethod, Type containerType)
    {
        public readonly Harmony HarmonyInstance = harmony;
        public readonly Type ContainerType = containerType;
        public readonly MethodBase? Original = original;
        public readonly MethodInfo? PatchMethod = patchMethod;
    }
    
    public void Commit(IRuntimePatchRegistry registry)
    {
        PatchFunctions.LoggerFunc = logger.LogDebug;

        try
        {
            foreach (var id in registry.GetIds())
            {
                if (!_harmonyInstances.TryGetValue(id, out var harmony))
                {
                    harmony = new Harmony(id);
                    _harmonyInstances.Add(id, harmony);
                }

                foreach (var original in registry.GetOriginalMethods())
                {
                    foreach (var patchMethod in registry.GetRemovedPrefixMethods(original, id))
                    {
                        logger.LogInformation("Unpatching {Original} prefix {Prefix}", original, patchMethod.method);
                        harmony.Unpatch(original, patchMethod.method);
                    }

                    foreach (var patchMethod in registry.GetRemovedPostfixMethods(original, id))
                    {
                        logger.LogInformation("Unpatching {Original} postfix {Postfix}", original, patchMethod.method);
                        harmony.Unpatch(original, patchMethod.method);
                    }

                    foreach (var patchMethod in registry.GetRemovedFinalizerMethods(original, id))
                    {
                        logger.LogInformation("Unpatching {Original} finalizer {Finalizer}", original, patchMethod.method);
                        harmony.Unpatch(original, patchMethod.method);
                    }

                    foreach (var patchMethod in registry.GetAddedPrefixMethods(original, id))
                    {
                        logger.LogInformation("Patching {Original} prefix {Prefix}", original, patchMethod.method);
                        PrepareCleanupPatchMethodBlock(registry, original, id, patchMethod, () =>
                        {
                            harmony.Patch(original, prefix: patchMethod);
                        });
                    }

                    foreach (var patchMethod in registry.GetAddedPostfixMethods(original, id))
                    {
                        logger.LogInformation("Patching {Original} postfix {Postfix}", original, patchMethod.method);
                        PrepareCleanupPatchMethodBlock(registry, original, id, patchMethod, () =>
                        {
                            harmony.Patch(original, postfix: patchMethod); 
                        });
                    }

                    foreach (var patchMethod in registry.GetAddedFinalizerMethods(original, id))
                    {
                        logger.LogInformation("Patching {Original} finalizer {Finalizer}", original, patchMethod.method);
                        PrepareCleanupPatchMethodBlock(registry, original, id, patchMethod, () =>
                        {
                            harmony.Patch(original, finalizer: patchMethod);
                        });
                    }
                }
            }

        }
        finally
        {
            PatchFunctions.LoggerFunc = null;
        }
    }
    
    private void PrepareContainerTypeBlock(IRuntimePatchRegistry registry, Type containerType, string id, Action action)
    {
        if (PrepareContainerType(registry, out var exception, containerType, id))
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
        
        CleanupContainerType(registry, containerType, exception, id);
    }
    
    public bool PrepareContainerType(IRuntimePatchRegistry registry, out Exception? exception, Type containerType, string id)
    {
        var harmony = _harmonyInstances[id];
        var context = new MethodCallContext(harmony, null, null!, containerType);
        try
        {
            exception = null;
            var callback = registry.GetPrepareContainerTypeCallback(containerType);
            return RunMethod<bool>(callback, true, false, context, parameters: [harmony]);
        }
        catch (Exception ex)
        {
            exception = ex;
            return false;
        }
    }

    public void CleanupContainerType(IRuntimePatchRegistry registry, Type containerType, Exception? exception, string id)
    {
        var harmony = _harmonyInstances[id];
        var context = new MethodCallContext(harmony, null, null!, containerType);
        try
        {
            var callback = registry.GetCleanupContainerTypeCallback(containerType);
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

    private void PrepareCleanupPatchMethodBlock(IRuntimePatchRegistry registry, MethodBase original, string id, HarmonyMethod patchMethod, Action action)
    {
        if (PreparePatchMethod(registry, out var exception, original, id, patchMethod))
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
                        
        CleanupPatchMethod(registry, original, exception, id, patchMethod);
    }
    
    public bool PreparePatchMethod(IRuntimePatchRegistry registry, out Exception? exception, MethodBase original, string id, HarmonyMethod patchMethod)
    {
        var harmony = _harmonyInstances[id];
        var context = new MethodCallContext(harmony, original, patchMethod.method, patchMethod.declaringType ?? patchMethod.method.DeclaringType!);
        try
        {
            exception = null;
            var callback = registry.GetCleanupPatchMethodCallback(patchMethod);
            return RunMethod<bool>(callback, true, false, context, parameters: [harmony, original]);
        }
        catch (Exception ex)
        {
            exception = ex;
            return false;
        }
    }
    
    public void CleanupPatchMethod(IRuntimePatchRegistry registry, MethodBase original, Exception? exception, string id, HarmonyMethod patchMethod)
    {
        var harmony = _harmonyInstances[id];
        var context = new MethodCallContext(harmony, original, patchMethod.method, patchMethod.declaringType ?? patchMethod.method.DeclaringType!);
        try
        {
            var callback = registry.GetCleanupPatchMethodCallback(patchMethod);
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
    private T RunMethod<T>(MethodInfo? callback, T defaultIfNotExisting, T defaultIfFailing, MethodCallContext context, Func<T, string>? failOnResult = null, params object[]? parameters)
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
    private void RunMethod(MethodInfo? callback, ref Exception? exception, MethodCallContext context, params object?[]? parameters)
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
    
    private void ReportException(Exception? exception, MethodCallContext context)
    {
        if (exception is null)
            return;
        if (Harmony.DEBUG)
        {
            _ = Harmony.VersionInfo(out var currentVersion);

            FileLog.indentLevel = 0;
            FileLog.Log($"### Exception from user \"{context.HarmonyInstance.Id}\", Harmony v{currentVersion}");
            FileLog.Log($"### Original: {(context.Original?.FullDescription() ?? "NULL")}");
            FileLog.Log($"### Patch class: {context.ContainerType.FullDescription()}");
            var logException = exception;
            if (logException is HarmonyException hEx)
                logException = hEx.InnerException;
            var exStr = logException.ToString();
            while (exStr.Contains("\n\n"))
                exStr = exStr.Replace("\n\n", "\n");
            exStr = exStr.Split('\n').Join(line => $"### {line}", "\n");
            FileLog.Log(exStr.Trim());
        }

        if (exception is HarmonyException)
            throw exception; // assume HarmonyException already wraps the actual exception
        throw new HarmonyException($"Patching exception in method {context.Original.FullDescription()}", exception);
    }
}