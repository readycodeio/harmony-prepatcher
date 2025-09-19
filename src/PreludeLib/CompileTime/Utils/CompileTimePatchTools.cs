using Mono.Cecil;
using PreludeLib.CompileTime.Public;

namespace PreludeLib.CompileTime.Utils;

public static class CompileTimePatchTools
{
    internal static List<CompileTimeAttributePatch> GetPatchMethods(TypeDefinition typeDef)
    {
        return [.. typeDef.Methods
            .Select(CompileTimeAttributePatch.Create)
            .Where(attributePatch => attributePatch is not null)!];
    }
	
    internal static MethodDefinition? GetPatchMethod(TypeDefinition patchTypeDef, string attributeName)
    {
        var method = patchTypeDef.Methods
            .FirstOrDefault(m => m.CustomAttributes.Any(a => a.AttributeType.FullName == attributeName));
        if (method is null)
        {
            // not-found is common and normal case, don't use AccessTools which will generate not-found warnings
            var methodName = attributeName.Replace("HarmonyLib.Harmony", "");
            method = patchTypeDef.Methods.FirstOrDefault(x => x.Name == methodName);
        }
        return method;
    }
}