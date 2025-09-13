using HarmonyLib;
using Mono.Cecil;
using Mono.Cecil.Rocks;
using PreludeLib.CompileTime.Public;
using PreludeLib.CompileTime.Utils;

namespace PreludeLib.CompileTime;

public class CompileTimePreludePatch(HarmonyPatchType patchType, CompileTimePreludeMethod patchMethod)
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

    public readonly HarmonyPatchType PatchType = patchType;
    public CompileTimePreludeMethod PatchMethod = patchMethod;

    public static CompileTimePreludePatch? Create(ModuleDefinition moduleDef, MethodDefinition patchMethodDef)
    {
        if (patchMethodDef is null)
            throw new NullReferenceException("Patch method cannot be null");

        var allAttributes = patchMethodDef.CustomAttributes.ToList();
        var p = patchMethodDef;
        while (p != null)
        {
            allAttributes.AddRange(p.CustomAttributes);
            p = p.GetBaseMethod();
        }
        
        var methodName = patchMethodDef.Name;
        var type = GetPatchType(methodName, allAttributes);
        if (type is null)
            return null;

        if (type != HarmonyPatchType.ReversePatch && patchMethodDef.IsStatic is false)
            throw new ArgumentException("Patch method " + patchMethodDef.FullDescription() + " must be static");

        var list = allAttributes
            .Where(attr => attr.GetType().BaseType?.FullName == typeof(HarmonyAttribute).FullName)
            .Select(attr =>
            {
                var f_info = AccessTools.Field(attr.GetType(), nameof(HarmonyAttribute.info));
                return f_info.GetValue(attr);
            })
            .Select(AccessTools.MakeDeepCopy<CompileTimePreludeMethod>)
            .ToList();
        var info = CompileTimePreludeMethod.Merge(list);
        info.Method = patchMethodDef;

        return new CompileTimePreludePatch(type.Value, info);
    }
    
    public static HarmonyPatchType? GetPatchType(string methodName, List<CustomAttribute> allAttributes)
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