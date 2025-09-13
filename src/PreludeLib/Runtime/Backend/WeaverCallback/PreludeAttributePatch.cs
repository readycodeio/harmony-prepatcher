using System.Reflection;
using HarmonyLib;

namespace PreludeLib.Runtime.Backend.WeaverCallback;

internal class PreludeAttributePatch
{
    static readonly HarmonyPatchType[] allPatchTypes = [
        HarmonyPatchType.Prefix,
        HarmonyPatchType.Postfix,
        HarmonyPatchType.Transpiler,
        HarmonyPatchType.Finalizer,
        HarmonyPatchType.ReversePatch,
        HarmonyPatchType.InnerPrefix,
        HarmonyPatchType.InnerPostfix
    ];

    internal HarmonyMethod Info;
    internal HarmonyPatchType? Type;

    internal static PreludeAttributePatch? Create(MethodInfo patch)
    {
        if (patch is null)
            throw new NullReferenceException("Patch method cannot be null");

        var allAttributes = patch.GetCustomAttributes(true);
        var methodName = patch.Name;
        var type = GetPatchType(methodName, allAttributes);
        if (type is null)
            return null;

        if (type != HarmonyPatchType.ReversePatch && patch.IsStatic is false)
            throw new ArgumentException($"Patch method {patch.FullDescription()} must be static");

        var list = allAttributes
            .Where(attr => attr.GetType().BaseType?.FullName == typeof(HarmonyAttribute).FullName)
            .Select(attr =>
            {
                var f_info = AccessTools.Field(attr.GetType(), nameof(HarmonyAttribute.info));
                return f_info.GetValue(attr);
            })
            .Select(AccessTools.MakeDeepCopy<HarmonyMethod>)
            .ToList();
        var info = HarmonyMethod.Merge(list);
        info.method = patch;

        return new PreludeAttributePatch()
        {
            Info = info,
            Type = type
        };
    }

    static HarmonyPatchType? GetPatchType(string methodName, object[] allAttributes)
    {
        var harmonyAttributes = new HashSet<string>(allAttributes
            .Select(attr => attr.GetType().FullName)
            .Where(name => name!.StartsWith("Harmony"))!);

        HarmonyPatchType? type = null;
        foreach (var patchType in allPatchTypes)
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