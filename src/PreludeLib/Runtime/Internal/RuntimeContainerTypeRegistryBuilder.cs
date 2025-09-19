using System.Reflection;
using HarmonyLib;
using PreludeLib.Runtime.Registry;

namespace PreludeLib.Runtime.Internal;

internal readonly struct RuntimeContainerTypeRegistryBuilder
{
    private class PatchJobs
    {
        internal class Job
        {
            internal PatchTarget target;
            internal List<HarmonyMethod> prefixes = [];
            internal List<HarmonyMethod> postfixes = [];
            internal List<HarmonyMethod> transpilers = [];
            internal List<HarmonyMethod> finalizers = [];
            internal List<HarmonyMethod> innerprefixes = [];
            internal List<HarmonyMethod> innerpostfixes = [];

            internal void AddPatch(AttributePatch patch)
            {
                switch (patch.type)
                {
                    case HarmonyPatchType.Prefix:
                        prefixes.Add(patch.info);
                        break;
                    case HarmonyPatchType.Postfix:
                        postfixes.Add(patch.info);
                        break;
                    case HarmonyPatchType.Transpiler:
                        transpilers.Add(patch.info);
                        break;
                    case HarmonyPatchType.Finalizer:
                        finalizers.Add(patch.info);
                        break;
                    case HarmonyPatchType.InnerPrefix:
                        innerprefixes.Add(patch.info);
                        break;
                    case HarmonyPatchType.InnerPostfix:
                        innerpostfixes.Add(patch.info);
                        break;
                }
            }
        }

        private readonly Dictionary<PatchTarget, Job> _state = [];

        internal Job GetJob(PatchTarget target)
        {
            if (_state.TryGetValue(target, out var job) is false)
            {
                job = new Job() { target = target };
                _state[target] = job;
            }
            return job;
        }

        internal List<Job> GetJobs()
        {
            return [.. _state.Values.Where(job =>
                job.prefixes.Count +
                job.postfixes.Count +
                job.transpilers.Count +
                job.finalizers.Count +
                job.innerprefixes.Count +
                job.innerpostfixes.Count
                > 0
            )];
        }
    }
    
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

    internal RuntimeContainerTypeRegistryBuilder(RuntimeRegistryBuilder owner, Type type)
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

    private PatchGroup GetPatchGroup()
        => new(_containerType);
    
    public void Patch()
    {
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
        
        PatchTarget? lastTarget = null;
        var targets = GetBulkMethods();
        
        if (targets.Count == 1)
            lastTarget = targets[0];
        // NOTE: Skipping reverse patch feature
        // ReversePatch(ref lastOriginal);

        if (targets.Count > 0)
            BulkPatch(targets, ref lastTarget);
        else
            PatchWithAttributes(ref lastTarget);

        // NOTE: Skipped `HarmonyCleanup` feature
        // RunMethod<HarmonyCleanup>(ref exception, exception);
        // ReportException(exception, lastOriginal);
    }
    
    private void BulkPatch(List<PatchTarget> targets, ref PatchTarget? lastTarget)
    {
        var jobs = new PatchJobs();
        for (var i = 0; i < targets.Count; i++)
        {
            lastTarget = targets[i];
            var job = jobs.GetJob(lastTarget.Value);
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
            lastTarget = job.target;
            ProcessPatchJob(job);
        }
    }
    
    private void PatchWithAttributes(ref PatchTarget? lastTarget)
    {
        var jobs = new PatchJobs();
        foreach (var patchMethod in _patchMethods)
        {
            lastTarget = PatchTarget.FromOriginal(patchMethod.info.GetOriginalMethod(), GetPatchGroup());
            if (lastTarget is null)
                throw new ArgumentException($"Undefined target method for patch method {patchMethod.info.method.FullDescription()}");

            var job = jobs.GetJob(lastTarget.Value);
            job.AddPatch(patchMethod);
        }
        foreach (var job in jobs.GetJobs())
        {
            lastTarget = job.target;
            ProcessPatchJob(job);
        }
    }
    
    private void ProcessPatchJob(PatchJobs.Job job)
    {
        // NOTE: Skipped `HarmonyPrepare` feature
        // var individualPrepareResult = RunMethod<HarmonyPrepare, bool>(true, false, null, job.original);

        foreach (var prefix in job.prefixes)
        {
            _owner.Patch(job.target, prefix: prefix);
        }
        foreach (var postfix in job.postfixes)
        {
            _owner.Patch(job.target, postfix: postfix);
        }
        foreach (var transpiler in job.transpilers)
        {
            _owner.Patch(job.target, transpiler: transpiler);
        }
        foreach (var finalizer in job.finalizers)
        {
            _owner.Patch(job.target, finalizer: finalizer);
        }
        
        if (job.innerprefixes.Count > 0)
            throw new NotImplementedException("InnerPrefix is not implemented");
        if (job.innerpostfixes.Count > 0)
            throw new NotImplementedException("InnerPostfix is not implemented");
        
        // RunMethod<HarmonyCleanup>(ref exception, job.original, exception);
        // ReportException(exception, job.original);
    }

    private List<PatchTarget> GetBulkMethods()
    {
        var isPatchAll = _containerType.GetCustomAttributes(true).Any(a => a.GetType().FullName == PatchTools.harmonyPatchAllFullName);
        if (isPatchAll)
        {
            var type = _containerAttributes.declaringType;
            if (type is null)
                throw new ArgumentException($"Using {PatchTools.harmonyPatchAllFullName} requires an additional attribute for specifying the Class/Type");

            var list = new List<PatchTarget>();
            var group = GetPatchGroup();
            list.AddRange(AccessTools.GetDeclaredConstructors(type).Cast<MethodBase>().Select(x => PatchTarget.FromOriginal(x, group)));
            list.AddRange(AccessTools.GetDeclaredMethods(type).Cast<MethodBase>().Select(x => PatchTarget.FromOriginal(x, group)));
            var props = AccessTools.GetDeclaredProperties(type);
            list.AddRange(props.Select(prop => prop.GetGetMethod(true)).Where(method => method is not null).Cast<MethodBase>().Select(x => PatchTarget.FromOriginal(x, group)));
            list.AddRange(props.Select(prop => prop.GetSetMethod(true)).Where(method => method is not null).Cast<MethodBase>().Select(x => PatchTarget.FromOriginal(x, group)));
            return list;
        }

        var result = new List<MethodBase>();

        if (_auxiliaryMethods.TryGetValue(typeof(HarmonyTargetMethods), out var harmonyTargetListMethod))
        {
            return [PatchTarget.FromTargetMethod(harmonyTargetListMethod, GetPatchGroup())];
        }

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

        if (_auxiliaryMethods.TryGetValue(typeof(HarmonyTargetMethod), out var harmonyTargetMethod))
        {
            return [PatchTarget.FromTargetMethod(harmonyTargetMethod, GetPatchGroup())];
        }

        // var targetMethod = RunMethod<HarmonyTargetMethod, MethodBase>(null, null, method => method is null ? "null" : null);
        /*
        if (targetMethod is not null)
            result.Add(targetMethod);
        */

        return [];
    }
}