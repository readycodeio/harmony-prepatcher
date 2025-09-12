using System.Collections.Concurrent;
using System.Reflection;
using System.Reflection.Emit;
using PreludeLib.Runtime.WeaverCallback;

namespace PreludeLib.Runtime.Utils;

public static class MethodUtils
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
    
    private static readonly Dictionary<ConstructorInfo, MethodInfo> _constructorCache = new();
    
    public static MethodInfo WrapConstructor(ConstructorInfo ctor)
    {
        if (_constructorCache.TryGetValue(ctor, out var cached))
            return cached;
        
        var paramTypes = ctor.GetParameters().Select(p => p.ParameterType).ToArray();
        var declaring = ctor.DeclaringType!;

        var dm = new DynamicMethod(
            name: $"{declaring.Name}_ctor_wrapper",
            returnType: declaring,
            parameterTypes: paramTypes,
            m: declaring.Module,
            skipVisibility: true);

        var il = dm.GetILGenerator();

        // Push all arguments
        for (var i = 0; i < paramTypes.Length; i++)
        {
            il.Emit(OpCodes.Ldarg_S, (short)i);
        }

        il.Emit(OpCodes.Newobj, ctor);
        il.Emit(OpCodes.Ret);

        _constructorCache.Add(ctor, dm);
        return dm; // This is a MethodInfo
    }

    public static MethodInfo WrapMethod(MethodBase method)
    {
        if (method is MethodInfo mi)
            return mi;
        else if (method is ConstructorInfo ci)
            return WrapConstructor(ci);
        else
            throw new NotSupportedException("Only methods and constructors can be wrapped");
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
    
    public static Type GetOrCreateDelegateType(MethodInfo methodInfo)
    {
        var key = GetCacheKey(methodInfo);

        if (_delegateTypeCache.TryGetValue(key, out var result))
            return result;
        
        var typeName = "Del_" + Guid.NewGuid().ToString("N");
        var delTypeBuilder = _cacheModule.DefineType(
            typeName,
            TypeAttributes.Class | TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.AnsiClass | TypeAttributes.AutoClass,
            typeof(MulticastDelegate));

        // Standard delegate .ctor(object, IntPtr)
        var ctorBuilder = delTypeBuilder.DefineConstructor(
            MethodAttributes.RTSpecialName | MethodAttributes.Public | MethodAttributes.HideBySig,
            CallingConventions.Standard,
            [typeof(object), typeof(IntPtr)]);
        ctorBuilder.SetImplementationFlags(MethodImplAttributes.Runtime | MethodImplAttributes.Managed);

        // Invoke method with proper signature (supports ref/out/in via ByRef parameter types)
        var invokeMethodBuilder = delTypeBuilder.DefineMethod(
            "Invoke",
            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.NewSlot,
            key.ReturnType,
            key.ParamTypes);

        // Apply in/out flags to parameters (purely descriptive; ByRef-ness is already in paramTypes)
        for (var i = 0; i < key.ParamTypes.Length; i++)
        {
            invokeMethodBuilder.DefineParameter(i + 1, key.ParamAttributes[i], strParamName: null);
        }

        // Mark as runtime-implemented (delegate invocation handled by CLR)
        invokeMethodBuilder.SetImplementationFlags(MethodImplAttributes.Runtime | MethodImplAttributes.Managed);

        result = delTypeBuilder.CreateTypeInfo().AsType();
        return _delegateTypeCache.GetOrAdd(key, result);
    }
    
    public static Delegate CreateDelegate(MethodInfo methodInfo)
    {
        var delType = GetOrCreateDelegateType(methodInfo);
        return methodInfo.CreateDelegate(delType);
    }
}