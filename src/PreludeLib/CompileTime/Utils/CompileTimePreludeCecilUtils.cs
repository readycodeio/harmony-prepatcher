using System.Text;
using HarmonyLib;
using Mono.Cecil;
using Mono.Cecil.Rocks;
using PreludeLib.CompileTime.Public;

namespace PreludeLib.CompileTime.Utils;

public static class CompileTimePreludeCecilUtils
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
        => typeDef.CustomAttributes.Any(x =>
        {
	        var f = CompileTimeAccessTools.Field(x.AttributeType.Resolve(), nameof(HarmonyAttribute.info));
		    if (f == null)
			    return false;
	        return f.FieldType.FullName == typeof(HarmonyMethod).FullName;
        });
    
    public static List<CompileTimePreludePatch> GetPatches(ModuleDefinition moduleDef, TypeDefinition typeDef)
    {
        return [.. typeDef.Methods
            .Select(CompileTimePreludePatch.Create)
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
    
    public static TypeReference? GetReturnedType(MethodDefinition? methodOrConstructor)
    {
        if (methodOrConstructor is null)
        {
            FileLog.Debug("AccessTools.GetReturnedType: methodOrConstructor is null");
            return null;
        }
        return methodOrConstructor.ReturnType ?? methodOrConstructor.Module.TypeSystem.Void;
    }
    
    public static IEnumerable<HarmonyArgument> GetArgumentAttributes(MethodReference methodRef)
    {
	    try
	    {
		    var methodDef = methodRef.Resolve();
		    var attributes = methodDef.CustomAttributes;
		    return AllHarmonyArguments(attributes);
	    }
	    catch (NotSupportedException)
	    {
		    return [];
	    }
    }
    
    public static IEnumerable<HarmonyArgument> GetArgumentAttributes(TypeReference typeRef)
    {
	    try
	    {
		    var typeDef = typeRef.Resolve();
		    var attributes = typeDef.CustomAttributes;
		    return AllHarmonyArguments(attributes);
	    }
	    catch (NotSupportedException)
	    {
		    return [];
	    }
    }
    
    public static HarmonyArgument? GetArgumentAttribute(ParameterDefinition parameterDef)
    {
	    try
	    {
		    var attributes = parameterDef.CustomAttributes;
		    return AllHarmonyArguments(attributes).FirstOrDefault();
	    }
	    catch (NotSupportedException)
	    {
		    return null;
	    }
    }
    
    public static IEnumerable<HarmonyArgument> AllHarmonyArguments(IEnumerable<CustomAttribute> attributes)
    {
	    return attributes.Select(attr =>
		    {
			    if (attr.AttributeType.Name != nameof(HarmonyArgument)) return null;
			    return Activator.CreateInstance(typeof(HarmonyArgument), [..attr.ConstructorArguments.Select(x => x.Value)]);
		    })
		    .OfType<HarmonyArgument>();
    }
    
    public static int GetArgumentIndex(MethodDefinition patch, string[] originalParameterNames, ParameterReference patchParam)
    {
	    var originalName = GetRealParameterName(patchParam, originalParameterNames);
	    if (originalName is not null)
		    return Array.IndexOf(originalParameterNames, originalName);

	    originalName = GetRealParameterName(patch, originalParameterNames, patchParam.Name);
	    if (originalName is not null)
		    return Array.IndexOf(originalParameterNames, originalName);

	    return -1;
    }
    
    public static string? GetRealParameterName(ParameterReference parameterRef, string[] originalParameterNames)
    {
	    var parameterDef = parameterRef.Resolve();
	    var attribute = GetArgumentAttribute(parameterDef);
	    if (attribute is null)
		    return null;

	    if (string.IsNullOrEmpty(attribute.OriginalName) is false)
		    return attribute.OriginalName;

	    if (attribute.Index >= 0 && attribute.Index < originalParameterNames.Length)
		    return originalParameterNames[attribute.Index];

	    return null;
    }
    
    public static string? GetRealParameterName(MethodReference? methodRef, string[] originalParameterNames, string name)
    {
	    if (methodRef is null)
		    return name;

	    var argumentName = GetArgumentAttributes(methodRef).GetRealName(name, originalParameterNames);
	    if (argumentName is not null)
		    return argumentName;

	    var typeRef = methodRef.DeclaringType;
	    if (typeRef is not null)
	    {
		    argumentName = GetArgumentAttributes(typeRef).GetRealName(name, originalParameterNames);
		    if (argumentName is not null)
			    return argumentName;
	    }

	    return name;
    }

	internal static List<CompileTimePreludePatch> GetPatchMethods(TypeDefinition typeDef)
	{
	    return [.. typeDef.Methods
		    .Select(CompileTimePreludePatch.Create)
		    .Where(attributePatch => attributePatch is not null)!];
    }
}