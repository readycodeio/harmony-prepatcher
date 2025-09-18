using System.Reflection;
using HarmonyLib;
using PreludeLib.Runtime.Utils;

namespace PreludeLib.Runtime.Internal;

internal readonly struct RuntimeContainerTypeRegistryBuilder
{
    private static readonly List<Type> _auxiliaryTypes =
    [
        typeof(HarmonyPrepare),
        typeof(HarmonyCleanup),
        typeof(HarmonyTargetMethod),
        typeof(HarmonyTargetMethods)
    ];
    
    private readonly RuntimeRegistryBuilder _owner;
    private readonly Type _containerType;
    private readonly HarmonyMethod _containerAttributes;
    private readonly Dictionary<Type, MethodInfo> _auxiliaryMethods;
    private readonly List<AttributePatch> _patchMethods;
    
    public RuntimeContainerTypeRegistryBuilder(RuntimeRegistryBuilder owner, Type type)
    {
        _owner = owner;
        _containerType = type;
        
        var harmonyAttributes = HarmonyMethodExtensions.GetFromType(type);
        _containerAttributes = HarmonyMethod.Merge(harmonyAttributes);
        _containerAttributes.methodType ??= MethodType.Normal;

        _auxiliaryMethods = [];
        foreach (var auxType in _auxiliaryTypes)
        {
            var method = PatchTools.GetPatchMethod(_containerType, auxType.FullName);
            if (method is not null)
                _auxiliaryMethods[auxType] = method;
        }

        _patchMethods = PatchTools.GetPatchMethods(_containerType);
        foreach (var patchMethod in _patchMethods)
        {
            var method = patchMethod.info.method;
            patchMethod.info = _containerAttributes.Merge(patchMethod.info);
            patchMethod.info.method = method;
        }
    }

    public void Patch()
    {
        // Exception? exception = null;

        // NOTE: Skipped `HarmonyPrepare` feature
        /*
        var mainPrepareResult = RunMethod<HarmonyPrepare, bool>(true, false);
        if (mainPrepareResult is false)
        {
            RunMethod<HarmonyCleanup>(ref exception);
            ReportException(exception, null);
            return [];
        }
        */
        
        var replacements = new List<MethodInfo>();
        MethodBase? lastOriginal = null;
        try
        {
            var originals = GetBulkMethods();

            if (originals.Count == 1)
                lastOriginal = originals[0];
            // NOTE: Skipping reverse patch feature
            // ReversePatch(ref lastOriginal);

            if (originals.Count > 0)
                BulkPatch(originals, ref lastOriginal);
            else
                PatchWithAttributes(ref lastOriginal);
        }
        catch (Exception ex)
        {
            // exception = ex;
        }

        // NOTE: Skipped `HarmonyCleanup` feature
        // RunMethod<HarmonyCleanup>(ref exception, exception);
        // ReportException(exception, lastOriginal);
    }
    
    private List<MethodInfo> BulkPatch(List<MethodBase> originals, ref MethodBase? lastOriginal)
    {
        var jobs = new PatchJobs<MethodInfo>();
        for (var i = 0; i < originals.Count; i++)
        {
            lastOriginal = originals[i];
            var job = jobs.GetJob(lastOriginal);
            foreach (var patchMethod in _patchMethods)
            {
                var note = "You cannot combine TargetMethod, TargetMethods or [HarmonyPatchAll] with individual annotations";
                var info = patchMethod.info;
                if (info.methodName is not null)
                    throw new ArgumentException($"{note} [{info.methodName}]");
                if (info.methodType.HasValue && info.methodType.Value != MethodType.Normal)
                    throw new ArgumentException($"{note} [{info.methodType}]");
                if (info.argumentTypes is not null)
                    throw new ArgumentException($"{note} [{info.argumentTypes.Description()}]");

                job.AddPatch(patchMethod);
            }
        }
        foreach (var job in jobs.GetJobs())
        {
            lastOriginal = job.original;
            ProcessPatchJob(job);
        }
        return jobs.GetReplacements();
    }
    
    private List<MethodInfo> PatchWithAttributes(ref MethodBase? lastOriginal)
    {
        var jobs = new PatchJobs<MethodInfo>();
        foreach (var patchMethod in _patchMethods)
        {
            lastOriginal = patchMethod.info.GetOriginalMethod();
            if (lastOriginal is null)
                throw new ArgumentException($"Undefined target method for patch method {patchMethod.info.method.FullDescription()}");

            var job = jobs.GetJob(lastOriginal);
            job.AddPatch(patchMethod);
        }
        foreach (var job in jobs.GetJobs())
        {
            lastOriginal = job.original;
            ProcessPatchJob(job);
        }
        return jobs.GetReplacements();
    }
    
    private void ProcessPatchJob(PatchJobs<MethodInfo>.Job job)
    {
        // MethodInfo? replacement = null;

        // NOTE: Skipped `HarmonyPrepare` feature
        // var individualPrepareResult = RunMethod<HarmonyPrepare, bool>(true, false, null, job.original);
        // Exception? exception = null;

        // if (individualPrepareResult)
        {
            // lock (PatchProcessor.locker)
            {
                try
                {
                    foreach (var prefix in job.prefixes)
                    {
                        _owner.Patch(job.original, prefix: prefix);
                    }
                    foreach (var postfix in job.postfixes)
                    {
                        _owner.Patch(job.original, postfix: postfix);
                    }
                    foreach (var transpiler in job.transpilers)
                    {
                        _owner.Patch(job.original, transpiler: transpiler);
                    }
                    foreach (var finalizer in job.finalizers)
                    {
                        _owner.Patch(job.original, finalizer: finalizer);
                    }
                    
                    if (job.innerprefixes.Count > 0)
                        throw new NotImplementedException("InnerPrefix is not implemented in this backend");
                    if (job.innerpostfixes.Count > 0)
                        throw new NotImplementedException("InnerPostfix is not implemented in this backend");
                }
                catch (Exception ex)
                {
                    // exception = ex;
                }
            }
        }
        
        // NOTE: Skipped `HarmonyCleanup` feature
        // RunMethod<HarmonyCleanup>(ref exception, job.original, exception);
        // ReportException(exception, job.original);
        job.replacement = RuntimePreludeMethodUtils.WrapMethod(job.original);
    }

    private List<MethodBase> GetBulkMethods()
    {
        var isPatchAll = _containerType.GetCustomAttributes(true).Any(a => a.GetType().FullName == PatchTools.harmonyPatchAllFullName);
        if (isPatchAll)
        {
            var type = _containerAttributes.declaringType;
            if (type is null)
                throw new ArgumentException($"Using {PatchTools.harmonyPatchAllFullName} requires an additional attribute for specifying the Class/Type");

            var list = new List<MethodBase>();
            list.AddRange(AccessTools.GetDeclaredConstructors(type).Cast<MethodBase>());
            list.AddRange(AccessTools.GetDeclaredMethods(type).Cast<MethodBase>());
            var props = AccessTools.GetDeclaredProperties(type);
            list.AddRange(props.Select(prop => prop.GetGetMethod(true)).Where(method => method is not null).Cast<MethodBase>());
            list.AddRange(props.Select(prop => prop.GetSetMethod(true)).Where(method => method is not null).Cast<MethodBase>());
            return list;
        }

        var result = new List<MethodBase>();

        // NOTE: Skipped `HarmonyTarget` feature
        // var targetMethods = RunMethod<HarmonyTargetMethods, IEnumerable<MethodBase>>(null, null);
        /*
        if (targetMethods is object)
        {
            string error = null;
            result = [.. targetMethods];
            if (result is null)
                error = "null";
            else if (result.Any(m => m is null))
                error = "some element was null";
            if (error != null)
            {
                if (_auxiliaryMethods.TryGetValue(typeof(HarmonyTargetMethods), out var method))
                    throw new Exception($"Method {method.FullDescription()} returned an unexpected result: {error}");
                else
                    throw new Exception($"Some method returned an unexpected result: {error}");
            }
            return result;
        }
        */

        // var targetMethod = RunMethod<HarmonyTargetMethod, MethodBase>(null, null, method => method is null ? "null" : null);
        /*
        if (targetMethod is not null)
            result.Add(targetMethod);
        */

        return result;
    }
    
}