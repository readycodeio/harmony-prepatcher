using HarmonyLib;
using Microsoft.Extensions.Logging;
using PreludeLib.Runtime.Registry;

namespace PreludeLib.Runtime.Backend.HarmonyDetour;

public class PreludeHarmonyBackend(ILogger logger) : IRuntimeBackend
{
    private readonly Dictionary<string, Harmony> _harmonyInstances = [];

    public void Commit(IRuntimePatchRegistry registry)
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
                    harmony.Patch(original, prefix: patchMethod);
                }
                
                foreach (var patchMethod in registry.GetAddedPostfixMethods(original, id))
                {
                    logger.LogInformation("Patching {Original} postfix {Postfix}", original, patchMethod.method);
                    harmony.Patch(original, postfix: patchMethod);
                }
                
                foreach (var patchMethod in registry.GetAddedFinalizerMethods(original, id))
                {
                    logger.LogInformation("Patching {Original} finalizer {Finalizer}", original, patchMethod.method);
                    harmony.Patch(original, finalizer: patchMethod);
                }
            }
        }
    }
}