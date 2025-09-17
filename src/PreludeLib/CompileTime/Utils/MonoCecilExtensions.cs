using System.Diagnostics;
using Mono.Cecil;
using Mono.Cecil.Rocks;

namespace PreludeLib.CompileTime.Utils;

internal static class MonoCecilExtensions
{
    public static MethodReference ReplaceDeclaringType(this MethodReference methodRef, TypeReference typeRef)
    {
        var result = new MethodReference(
            methodRef.Name, 
            methodRef.ReturnType,
            typeRef
        );
        for (var i = 0; i < methodRef.Parameters.Count; i++)
        {
            result.Parameters.Add(methodRef.Parameters[i]);
        }
        result.CallingConvention = methodRef.CallingConvention;
        result.HasThis = methodRef.HasThis;
        result.ExplicitThis = methodRef.ExplicitThis;
        return result;
    }
    
    public static InterfaceMappingDefinition GetInterfaceMap(this TypeDefinition targetType, TypeDefinition interfaceType)
    {
        var interfaceMethods = new List<MethodDefinition>();
        var targetMethods = new List<MethodDefinition>();
        
        foreach (var interfaceMethod in interfaceType.Methods)
        {
            var hasExplicitOverride = false;
            MethodDefinition overrideMethod = null;
            foreach (var targetMethod in targetType.GetMethods())
            {
                var targetOverrides = targetMethod.Overrides;
                foreach (var targetOverride in targetOverrides)
                {
                    if (targetOverride.Equals(interfaceMethod))
                    {
                        hasExplicitOverride = true;
                        overrideMethod = targetMethod;
                        break;
                    }
                }

                if (hasExplicitOverride)
                    break;
            }

            if (!hasExplicitOverride)
            {
                foreach (var targetMethod in targetType.GetMethods())
                {
                    if (targetMethod.Name != interfaceMethod.Name)
                        continue;

                    var targetGenericParams = targetMethod.GenericParameters;
                    var interfaceGenericParams = targetMethod.GenericParameters;

                    if (targetGenericParams.Count != interfaceGenericParams.Count)
                        continue;

                    var targetParams = targetMethod.Parameters;
                    var interfaceParams = targetMethod.Parameters;

                    var paramsMatch = true;
                    for (var i = 0; i < targetParams.Count; i++)
                    {
                        var targetParam = targetParams[i];
                        var interfaceParam = interfaceParams[i];

                        if (targetParam.ParameterType != interfaceParam.ParameterType)
                        {
                            paramsMatch = false;
                            break;
                        }
                    }
                    
                    if (!paramsMatch)
                        continue;

                    overrideMethod = targetMethod;
                }
            }

            interfaceMethods.Add(interfaceMethod);
            Debug.Assert(overrideMethod != null);
            targetMethods.Add(overrideMethod);
        }
        
        return new InterfaceMappingDefinition(
            interfaceType, 
            interfaceMethods.ToArray(), 
            targetType, 
            targetMethods.ToArray()
        );
    }
}