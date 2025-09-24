using Microsoft.Extensions.Logging;
using Mono.Cecil;
using Mono.Cecil.Rocks;
using PreludeLib.Attributes;
using PreludeLib.CompileTime.Registry;
using PreludeLib.CompileTime.Utils;

namespace PreludeLib.CompileTime.Backend;

internal static class CompileTimeBackendUtils
{
    private static TypeReference FixRefInOut(TypeReference typeRef)
    {
        if (typeRef is GenericInstanceType instTypeRef)
        {
            var elemTypeRef = typeRef.GetElementType();

            if (elemTypeRef.FullName == typeof(Ref<>).FullName || elemTypeRef.FullName == typeof(In<>).FullName ||
                elemTypeRef.FullName == typeof(Out<>).FullName)
            {
                var arg = instTypeRef.GenericArguments[0];
                return arg.MakeByReferenceType();
            }
            else
            {
                return typeRef;
            }
        }
        else
        {
            return typeRef;
        }
    }

    public static IEnumerable<MethodDefinition> GetTargetOriginals(CompileTimePatchTarget target, CompileTimeAuxiliaryMethodContext context)
    {
        TypeReference? FindTypeByName(string typeFullName)
        {
            var asmResolver = target.Group.ContainerTypeDef!.Module.AssemblyResolver;

            TypeReference? SearchInModules(string name)
            {
                var typeInCurrentModule = target.Group.ContainerTypeDef!.Module.GetType(name);
                if (typeInCurrentModule != null)
                    return typeInCurrentModule;

                foreach (var asmRef in target.Group.ContainerTypeDef!.Module.AssemblyReferences)
                {
                    var asmDef = asmResolver.Resolve(asmRef);
                    foreach (var module in asmDef.Modules)
                    {
                        var foundType = module.GetType(name);
                        if (foundType != null)
                            return foundType;
                    }
                }

                return null;
            }

            // 1. Try a direct lookup. This works for non-nested types and nested types already using '/'
            var foundType = SearchInModules(typeFullName);
            if (foundType != null)
                return foundType;

            // 2. If not found, it might be a nested type using '.' notation.
            // We recursively try to find the outer class and then look for the nested type inside it.
            var lastDotIndex = typeFullName.LastIndexOf('.');
            if (lastDotIndex > 0)
            {
                var potentialOuterTypeName = typeFullName.Substring(0, lastDotIndex);
                var nestedTypeName = typeFullName.Substring(lastDotIndex + 1);

                var outerType = FindTypeByName(potentialOuterTypeName);

                if (outerType != null)
                {
                    var nestedType = outerType.Resolve().NestedTypes.FirstOrDefault(t => t.Name == nestedTypeName);
                    if (nestedType != null)
                    {
                        return nestedType;
                    }
                }
            }

            context.Logger.Log(LogLevel.Error, "Could not find type by name: {TypeName}", typeFullName);
            return null;
        }

        if (target.OriginalMethodDef != null)
        {
            return [target.OriginalMethodDef];
        }

        if (target.TargetMethodDef == null)
        {
            context.Logger.Log(LogLevel.Error, "Target method definition is null for target in group {GroupName}", target.Group.ContainerTypeDef.FullDescription());
            return [];
        }

        var originals = target.TargetMethodDef.CustomAttributes
            .Where(x => x.Constructor.DeclaringType.FullName == typeof(HarmonyTargetMethodHint).FullName)
            .Select(x =>
            {
                TypeReference? declaringType = null;
                string? methodName;
                TypeReference[] methodParams = [];

                if (x.ConstructorArguments.Count == 3)
                {
                    context.Logger.Log(LogLevel.Debug, "Processing [HarmonyTargetMethodHint] on {GroupName}", target.Group.ContainerTypeDef.FullDescription());

                    if (x.ConstructorArguments[0].Type.FullName == typeof(Type).FullName)
                    {
                        // Constructor: (Type declaringType, string methodName, params Type[] args)
                        declaringType = (TypeReference)x.ConstructorArguments[0].Value;
                    }
                    else
                    {
                        // Constructor: (string declaringType, string methodName, params Type[] args)
                        if (x.ConstructorArguments[0].Value is string declaringTypeStr)
                            declaringType = FindTypeByName(declaringTypeStr);
                    }

                    methodName = x.ConstructorArguments[1].Value as string;
                    var args = x.ConstructorArguments[2].Value as CustomAttributeArgument[];
                    methodParams = args?.Select(a => (TypeReference)a.Value).ToArray() ?? [];
                }
                else
                {
                    context.Logger.Log(LogLevel.Error, "Invalid number of arguments in HarmonyTargetMethodHint attribute on method {Method} in group {GroupName}", target.TargetMethodDef.FullDescription(), target.Group.ContainerTypeDef.FullDescription());
                    return null;
                }

                if (declaringType == null || methodName == null)
                {
                    context.Logger.Log(LogLevel.Error, "Could not resolve declaring type or method name in HarmonyTargetMethodHint attribute on method {Method} in group {GroupName}", target.TargetMethodDef.FullDescription(), target.Group.ContainerTypeDef.FullDescription());
                    return null;
                }

                for (var i = 0; i < methodParams.Length; i++)
                {
                    methodParams[i] = FixRefInOut(methodParams[i]);
                }

                var overloads = declaringType.Resolve().Methods
                    .Where(m => m.Name == methodName)
                    .ToArray();

                if (overloads.Length == 1)
                    return overloads[0];

                // when there are multiple overloads, we need to match the parameters
                return overloads.FirstOrDefault(m =>
                {
                    if (m.Parameters.Count != methodParams.Length)
                        return false;

                    for (var i = 0; i < m.Parameters.Count; i++)
                    {
                        if (m.Parameters[i].ParameterType.FullName != methodParams[i].FullName)
                        {
                            return false;
                        }
                    }

                    return true;
                });
            });

        return originals.Where(m => m != null)!;
    }
}