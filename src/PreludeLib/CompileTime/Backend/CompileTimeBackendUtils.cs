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
        if (target.OriginalMethodDef != null)
        {
            return [target.OriginalMethodDef];
        }
        else if (target.TargetMethodDef != null)
        {
            var originals = target.TargetMethodDef.CustomAttributes.Where(x =>
                    x.Constructor.DeclaringType.FullName == typeof(HarmonyTargetMethodHint).FullName)
                .Select(x =>
                {
                    TypeReference? declaringType = null;
                    string? methodName;
                    TypeReference[] methodParams;

                    if (x.ConstructorArguments.Count == 3)
                    {
                        if (x.ConstructorArguments[0].Type.FullName == typeof(Type).FullName)
                        {
                            declaringType = (TypeReference)x.ConstructorArguments[0].Value;
                        }
                        else
                        {
                            var declaringTypeStr = x.ConstructorArguments[0].Value as string;
                            var asmResolver = target.Group.ContainerTypeDef!.Module.AssemblyResolver;
                            foreach (var asmRef in target.Group.ContainerTypeDef!.Module.AssemblyReferences)
                            {
                                var asmDef = asmResolver.Resolve(asmRef);
                                foreach (var module in asmDef.Modules)
                                {
                                    declaringType = module.GetType(declaringTypeStr);
                                    if (declaringType != null)
                                        break;
                                }
                                if (declaringType != null)
                                    break;
                            }
                        }
                        
                        methodName = x.ConstructorArguments[1].Value as string;
                        var args = x.ConstructorArguments[2].Value as CustomAttributeArgument[];
                        methodParams = args?.Select(a => (TypeReference)a.Value).ToArray() ?? [];

                    }
                    else if (x.ConstructorArguments.Count == 2)
                    {
                        declaringType = target.OriginalMethodsDeclaringTypeDef;
                        methodName = x.ConstructorArguments[0].Value as string;
                        var args = x.ConstructorArguments[1].Value as CustomAttributeArgument[];
                        methodParams = args?.Select(a => (TypeReference)a.Value).ToArray() ?? [];
                    }
                    else
                    {
                        return null;
                    }
                    
                    
                    for (var i = 0; i < methodParams.Length; i++)
                    {
                        methodParams[i] = FixRefInOut(methodParams[i]);
                    }

                    return declaringType?.Resolve().Methods.FirstOrDefault(m =>
                    {
                        if (m.Name != methodName)
                            return false;
                        if (m.Parameters.Count != methodParams.Length)
                            return false;
                        for (var i = 0; i < m.Parameters.Count; i++)
                        {
                            var param = m.Parameters[i];
                            var methodParam = methodParams[i];
                            if (param.ParameterType.FullName != methodParam.FullName)
                                return false;
                        }

                        return true;
                    });
                });

            return originals.Where(m => m != null)!;
        }
        else
        {
            return [];
        }
    }
}