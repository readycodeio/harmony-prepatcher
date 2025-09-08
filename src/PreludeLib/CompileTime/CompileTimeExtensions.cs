using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;

namespace PreludeLib.CompileTime;

public static class CompileTimeExtensions
{
    public static string FullDescription(this MethodReference? member)
        => (member?.Resolve()).FullDescription();

    public static string FullDescription(this MethodDefinition? member)
    {
        if (member is null)
            return "null";
        var returnType = member.ReturnType;

        var result = new StringBuilder();
        if (member.IsStatic)
            _ = result.Append("static ");
        if (member.IsAbstract)
            _ = result.Append("abstract ");
        if (member.IsVirtual)
            _ = result.Append("virtual ");
        _ = result.Append($"{returnType.FullDescription()} ");
        if (member.DeclaringType is not null)
            _ = result.Append($"{member.DeclaringType.FullDescription()}::");
        var parameterString = member.Parameters.Join(p => $"{p.ParameterType.FullDescription()} {p.Name}");
        _ = result.Append($"{member.Name}({parameterString})");
        return result.ToString();
    }

    public static string Join<T>(this IEnumerable<T> enumeration, Func<T, string>? converter = null, string delimiter = ", ")
    {
        converter ??= t => t!.ToString();
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