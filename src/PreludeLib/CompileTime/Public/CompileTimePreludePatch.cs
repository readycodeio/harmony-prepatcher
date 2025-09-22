extern alias OfficialCecil;
using HarmonyLib;
using OfficialCecil::Mono.Cecil;
using OfficialCecil::Mono.Cecil.Rocks;
using PreludeLib.CompileTime.Utils;

namespace PreludeLib.CompileTime.Public;

public class CompileTimeAttributePatch(HarmonyPatchType patchType, CompileTimePreludeMethod info)
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
    public CompileTimePreludeMethod Info = info;

    public static CompileTimeAttributePatch? Create(MethodDefinition patchMethodDef)
    {
        if (patchMethodDef is null)
            throw new NullReferenceException("Patch method cannot be null");

        var allAttributes = patchMethodDef.CustomAttributes.ToList();
        var p = patchMethodDef;
        while (true)
        {
            var q = p.GetBaseMethod();
            if (p == q)
                break;
            allAttributes.AddRange(q.CustomAttributes);
        }
        
        var methodName = patchMethodDef.Name;
        var type = GetPatchType(methodName, allAttributes);
        if (type is null)
            return null;

        if (type != HarmonyPatchType.ReversePatch && patchMethodDef.IsStatic is false)
            throw new ArgumentException($"Patch method {patchMethodDef.FullDescription()} must be static");

        var list = allAttributes
            .Where(attr => attr.AttributeType.Resolve().BaseType?.FullName == typeof(HarmonyAttribute).FullName)
            .Select(CompileTimePreludeMethodUtils.GetPreludeMethodInfo)
            .ToList();
        var info = CompileTimePreludeMethod.Merge(list);
        info.Method = patchMethodDef;

        return new CompileTimeAttributePatch(type.Value, info);
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