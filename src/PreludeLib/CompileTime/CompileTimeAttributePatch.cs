using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Mono.Cecil;
using Mono.Cecil.Rocks;

namespace PreludeLib.CompileTime;

public class CompileTimeAttributePatch
{
    private static readonly HarmonyPatchType[] AllPatchTypes = [
        HarmonyPatchType.Prefix,
        HarmonyPatchType.Postfix,
        HarmonyPatchType.Transpiler,
        HarmonyPatchType.Finalizer,
        HarmonyPatchType.ReversePatch,
        HarmonyPatchType.InnerPrefix,
        HarmonyPatchType.InnerPostfix
    ];

    internal CompileTimePreludeMethod? Info;
    internal HarmonyPatchType? Type;

    internal static CompileTimeAttributePatch? Create(ModuleDefinition module, MethodDefinition patch)
    {
        if (patch is null)
            throw new NullReferenceException("Patch method cannot be null");

        var allAttributes = patch.CustomAttributes.ToList();
        var p = patch;
        while (p != null)
        {
            allAttributes.AddRange(p.CustomAttributes);
            p = p.GetBaseMethod();
        }
        
        var methodName = patch.Name;
        var type = GetPatchType(methodName, allAttributes);
        if (type is null)
            return null;

        if (type != HarmonyPatchType.ReversePatch && patch.IsStatic is false)
            throw new ArgumentException("Patch method " + patch.FullDescription() + " must be static");

        var list = allAttributes
            .Where(attr => attr.GetType().BaseType?.FullName == CompileTimePatchTools.HarmonyAttributeFullName)
            .Select(attr =>
            {
                var f_info = AccessTools.Field(attr.GetType(), nameof(HarmonyAttribute.info));
                return f_info.GetValue(attr);
            })
            .Select(AccessTools.MakeDeepCopy<CompileTimePreludeMethod>)
            .ToList();
        var info = CompileTimePreludeMethod.Merge(list);
        info.Method = patch;

        return new CompileTimeAttributePatch()
        {
            Info = info,
            Type = type
        };
    }
    
    internal static HarmonyPatchType? GetPatchType(string methodName, List<CustomAttribute> allAttributes)
    {
        var harmonyAttributes = new HashSet<string>(allAttributes
            .Select(attr => attr.Constructor.DeclaringType.FullName)
            .Where(name => name!.StartsWith("Harmony")));

        HarmonyPatchType? type = null;
        foreach (var patchType in AllPatchTypes)
        {
            var name = patchType.ToString();
            if (name == methodName || harmonyAttributes.Contains($"HarmonyLib.Harmony{name}"))
            {
                type = patchType;
                break;
            }
        }
        return type;
    }
}