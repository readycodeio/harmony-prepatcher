using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Mono.Cecil;

namespace PreludeLib;

public static class CompileTimePatchTools
{
    internal static readonly string HarmonyMethodFullName = typeof(HarmonyMethod).FullName!;
    internal static readonly string HarmonyAttributeFullName = typeof(HarmonyAttribute).FullName!;
    internal static readonly string HarmonyPatchAllFullName = typeof(HarmonyPatchAll).FullName!;
    
    internal static List<CompileTimeAttributePatch> GetPatchMethods(ModuleDefinition module, TypeDefinition type)
    {
        return [.. type.Methods
            .Select(x => CompileTimeAttributePatch.Create(module, x))
            .Where(attributePatch => attributePatch is not null)];
    }
}