using System.Text;
using HarmonyLib;
using Mono.Cecil;

namespace PreludeLib.CompileTime.Utils;

public static class CompileTimeCecilExtensions
{
    public static IEnumerable<TypeDefinition> GetTypesFromAssemblyDef(AssemblyDefinition asmDef)
    {
        foreach (var moduleDef in asmDef.Modules)
        {
            foreach (var typeDef in moduleDef.Types)
            {
                yield return typeDef;
            }
        }
    }
    
    public static bool HasHarmonyAttributeDef(TypeDefinition typeDef)
        => typeDef.CustomAttributes.Any(x => x.Fields
            .Any(f => f.Name == nameof(HarmonyAttribute.info) && f.Argument.Type.FullName == typeof(HarmonyMethod).FullName));
    
    public static List<CompileTimePreludePatch> GetPatches(ModuleDefinition moduleDef, TypeDefinition typeDef)
    {
        return [.. typeDef.Methods
            .Select(x => CompileTimePreludePatch.Create(moduleDef, x))
            .Where(attributePatch => attributePatch is not null)!];
    }
    
    public static string Description(this IEnumerable<TypeReference>? parameterTypes)
    {
        if (parameterTypes is null)
            return "NULL";
        return $"({parameterTypes.Join(p => p.FullDescription())})";
    }
    
    public static string FullDescription(this MethodReference? methodRef)
        => (methodRef?.Resolve()).FullDescription();

    public static string FullDescription(this MethodDefinition? methodDef)
    {
        if (methodDef is null)
            return "null";
        var returnType = methodDef.ReturnType;

        var result = new StringBuilder();
        if (methodDef.IsStatic)
            _ = result.Append("static ");
        if (methodDef.IsAbstract)
            _ = result.Append("abstract ");
        if (methodDef.IsVirtual)
            _ = result.Append("virtual ");
        _ = result.Append($"{returnType.FullDescription()} ");
        if (methodDef.DeclaringType is not null)
            _ = result.Append($"{methodDef.DeclaringType.FullDescription()}::");
        var parameterString = methodDef.Parameters.Join(p => $"{p.ParameterType.FullDescription()} {p.Name}");
        _ = result.Append($"{methodDef.Name}({parameterString})");
        return result.ToString();
    }

    public static string Join<T>(this IEnumerable<T> enumeration, Func<T, string>? converter = null, string delimiter = ", ")
    {
        converter ??= t => t!.ToString()!;
        return enumeration.Aggregate("", (prev, curr) => prev + (prev.Length > 0 ? delimiter : "") + converter(curr));
    }
    
    public static string FullDescription(this TypeReference? typeDef)
    {
        if (typeDef is null)
            return "null";

        var ns = typeDef.Namespace;
        if (string.IsNullOrEmpty(ns) is false)
            ns += ".";
        var result = ns + typeDef.Name;

        if (typeDef is GenericInstanceType genericInstance)
        {
            result += "<";
            var subTypes = genericInstance.GenericArguments;
            for (var i = 0; i < subTypes.Count; i++)
            {
                if (result
#if NET8_0_OR_GREATER
					.EndsWith('<')
#else
                        .EndsWith("<", StringComparison.Ordinal)
#endif
                    is false)
                    result += ", ";
                result += subTypes[i].FullDescription();
            }
            result += ">";
        }
        return result;
    }
}