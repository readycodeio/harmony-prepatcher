using System.Collections.Concurrent;
using System.Reflection;
using System.Reflection.Emit;
using PreludeLib.Attributes;
using PreludeLib.Runtime.Backend.WeaverCallback;

namespace PreludeLib.Runtime.Utils;

public static class RuntimePreludeMethodUtils
{
    private readonly struct CacheKey(Type returnType, Type[] paramTypes, ParameterAttributes[] paramAttributes) : IEquatable<CacheKey>
    {
        public readonly Type ReturnType = returnType;
        public readonly Type[] ParamTypes = paramTypes;
        public readonly ParameterAttributes[] ParamAttributes = paramAttributes;

        public bool Equals(CacheKey other)
        {
            if (ReturnType != other.ReturnType)
                return false;
            if (ParamTypes.Length != other.ParamTypes.Length)
                return false;
            if (ParamAttributes.Length != other.ParamAttributes.Length)
                return false;
            
            for (var i = 0; i < ParamTypes.Length; i++)
            {
                var paramType = ParamTypes[i];
                var otherParamType = other.ParamTypes[i];
                if (paramType != otherParamType)
                    return false;
                var paramAttr = ParamAttributes[i];
                var otherParamAttr = other.ParamAttributes[i];
                if (paramAttr != otherParamAttr)
                    return false;
            }
            
            return true;
        }

        public override bool Equals(object? obj)
            => obj is CacheKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = ReturnType.GetHashCode();
                hashCode = (hashCode * 397) ^ ParamTypes.Length;
                hashCode = (hashCode * 397) ^ ParamAttributes.Length;
                for (var i = 0; i < ParamTypes.Length; i++)
                {
                    hashCode = (hashCode * 397) ^ ParamTypes[i].GetHashCode();
                    hashCode = (hashCode * 397) ^ ParamAttributes[i].GetHashCode();
                }
                return hashCode;
            }
        }
    }
    
    private static readonly AssemblyBuilder _cacheAssembly =
        AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("DynamicDelegates"), AssemblyBuilderAccess.Run);
    private static readonly ModuleBuilder _cacheModule = _cacheAssembly.DefineDynamicModule("DynamicDelegates");

    private static readonly ConcurrentDictionary<CacheKey, Type> _delegateTypeCache = new();

    private static CacheKey GetCacheKey(MethodInfo method)
    {
        var parameters = method.GetParameters();

        var list = parameters.Select(p =>
        {
            var paramType = p.ParameterType;
            if (paramType.IsGenericType && paramType.GetGenericTypeDefinition() == typeof(Ref<>))
                paramType = paramType.GetGenericArguments()[0].MakeByRefType();
            return paramType;
        }).ToList();
        var attrs = parameters.Select(GetParamAttributes).ToList();

        if (!method.IsStatic)
            throw new NotSupportedException("Instance methods are not supported");

        return new CacheKey(method.ReturnType, list.ToArray(), attrs.ToArray());

        static ParameterAttributes GetParamAttributes(ParameterInfo p)
        {
            var a = ParameterAttributes.None;
            if (p.IsIn)  a |= ParameterAttributes.In;
            if (p.IsOut) a |= ParameterAttributes.Out;
            return a;
        }
    }
}