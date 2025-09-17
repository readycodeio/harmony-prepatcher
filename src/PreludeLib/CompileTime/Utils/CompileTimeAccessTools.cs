using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace PreludeLib.CompileTime.Utils;

public static class CompileTimeAccessTools
{
    public static MethodReference? DeclaredMethod(TypeReference? typeRef, string name, TypeReference[]? parameterRefs = null, TypeReference[]? genericsRefs = null)
    {
        var typeDef = typeRef?.Resolve();
        return DeclaredMethod(typeDef, name, parameterRefs, genericsRefs);
    }

    public static MethodReference? DeclaredMethod(TypeDefinition? typeDef, string name, TypeReference[]? parameterRefs = null, TypeReference[]? genericsRefs = null)
    {
        if (typeDef is null)
            return null;

        if (string.IsNullOrEmpty(name))
            return null;

        MethodReference? result;

        if (parameterRefs is null)
            result = typeDef.GetMethods().FirstOrDefault(x =>
            {
                if (x.Name != name)
                    return false;
                return true;
            });
        else
            result = typeDef.GetMethods().FirstOrDefault(x =>
            {
                if (x.Name != name)
                    return false;
                if (x.Parameters.Count != parameterRefs.Length)
                    return false;
                for (var i = 0; i < parameterRefs.Length; i++)
                {
                    var xParamTypeRef = x.Parameters[i].ParameterType;
                    var paramTypeRef = parameterRefs[i];
                    if (xParamTypeRef.Resolve() != paramTypeRef.Resolve())
                        return false;
                }
                return true;
            });
        
        if (result is null)
            return null;

        if (genericsRefs is not null)
        {
            var inst = new GenericInstanceMethod(result);
            foreach (var x in genericsRefs)
            {
                inst.GenericArguments.Add(x);
            }
            result = inst;
        }
        return result;
    }

    public static MethodDefinition? DeclaredIndexerGetter(TypeReference? typeRef, TypeReference[]? parameterRefs)
        => DeclaredIndexer(typeRef, parameterRefs)?.Resolve().GetMethod;

    public static MethodDefinition? DeclaredPropertyGetter(TypeReference? typeRef, string name)
        => DeclaredProperty(typeRef, name)?.Resolve().GetMethod;

    public static MethodDefinition? DeclaredIndexerSetter(TypeReference? typeRef, TypeReference[]? parameterRefs)
        => DeclaredIndexer(typeRef, parameterRefs)?.Resolve().SetMethod;

    public static MethodDefinition? DeclaredPropertySetter(TypeReference? typeRef, string name)
        => DeclaredProperty(typeRef, name)?.Resolve().SetMethod;

    public static MethodDefinition? DeclaredConstructor(TypeReference? typeRef, TypeReference[]? parameterRefs)
    {
        var typeDef = typeRef?.Resolve();
        return DeclaredConstructor(typeDef, parameterRefs);
    }

    public static MethodDefinition? DeclaredConstructor(TypeDefinition? typeDef, TypeReference[]? parameterRefs)
    {
        if (typeDef is null)
            return null;
        parameterRefs ??= [];
        return typeDef.GetMethods().FirstOrDefault(x =>
        {
            if (x.Name != ".ctor")
                return false;
            if (x.Parameters.Count != parameterRefs.Length)
                return false;
            for (var i = 0; i < parameterRefs.Length; i++)
            {
                var xParamTypeRef = x.Parameters[i].ParameterType;
                var paramTypeRef = parameterRefs[i];
                if (xParamTypeRef.Resolve() != paramTypeRef.Resolve())
                    return false;
            }
            return true;
        });
    }

    public static IEnumerable<MethodDefinition> GetDeclaredConstructors(TypeReference? typeRef)
    {
        var typeDef = typeRef?.Resolve();
        return GetDeclaredConstructors(typeDef);
    }
    
    public static IEnumerable<MethodDefinition> GetDeclaredConstructors(TypeDefinition? typeDef)
    {
        if (typeDef is null)
            return [];
        return typeDef.GetMethods().Where(x => x.Name == ".ctor");
    }

    public static MethodReference? EnumeratorMoveNext(MethodReference? methodRef)
    {
        var methodDef = methodRef?.Resolve();
        return EnumeratorMoveNext(methodDef);
    }

    public static MethodReference? EnumeratorMoveNext(MethodDefinition? methodDef)
    {
        if (methodDef is null)
            return null;

        var codes = methodDef.Body.Instructions.Where(instr => instr.OpCode == OpCodes.Newobj).ToList();
        if (codes.Count != 1)
            return null;
        var ctor = codes.FirstOrDefault()?.Operand as MethodReference;
        if (ctor == null)
            return null;
        var type = ctor.DeclaringType;
        if (type == null)
            return null;
        return Method(type, nameof(IEnumerator.MoveNext));
    }

    public static MethodReference? AsyncMoveNext(MethodReference? methodRef)
    {
        var methodDef = methodRef?.Resolve();
        return AsyncMoveNext(methodDef);
    }

    public static MethodReference? AsyncMoveNext(MethodDefinition? methodDef)
    {
        if (methodDef is null)
            return null;

        var asyncAttribute = methodDef.CustomAttributes.FirstOrDefault(x => x.AttributeType.Name == nameof(AsyncStateMachineAttribute));
        if (asyncAttribute is null)
            return null;

        var asyncStateMachineType = asyncAttribute.Fields.FirstOrDefault(x => x.Name == nameof(StateMachineAttribute.StateMachineType)).Argument.Value as TypeReference;
        var asyncMethodBody = DeclaredMethod(asyncStateMachineType, nameof(IAsyncStateMachine.MoveNext));
        if (asyncMethodBody is null)
            return null;

        return asyncMethodBody;
    }

    public static MethodReference? DeclaredFinalizer(TypeReference? typeRef)
        => DeclaredMethod(typeRef, "Finalize");

    public static MethodDefinition? DeclaredEventAdder(TypeReference? typeRef, string name)
        => DeclaredEvent(typeRef, name)?.AddMethod;

    public static MethodDefinition? DeclaredEventRemover(TypeReference? typeRef, string name)
        => DeclaredEvent(typeRef, name)?.RemoveMethod;

    public static PropertyDefinition? DeclaredIndexer(TypeReference? typeRef, TypeReference[]? parameterRefs = null)
    {
        var typeDef = typeRef?.Resolve();
        return DeclaredIndexer(typeDef, parameterRefs);
    }

    public static PropertyDefinition? DeclaredIndexer(TypeDefinition? typeDef, TypeReference[]? parameterRefs = null)
    {
        if (typeDef is null)
            return null;

        try
        {
            // Can find multiple indexers without specified parameters, but only one with specified ones
            var indexer = parameterRefs is null ?
                typeDef.Properties.SingleOrDefault(property => property.Parameters.Count > 0)
                : typeDef.Properties.FirstOrDefault(property => property.Parameters.Select(param => param.ParameterType).SequenceEqual(parameterRefs));

            return indexer;
        }
        catch (InvalidOperationException ex)
        {
            throw new AmbiguousMatchException("Multiple possible indexers were found.", ex);
        }
    }

    public static PropertyDefinition? DeclaredProperty(TypeReference? typeRef, string name)
    {
        var typeDef  = typeRef?.Resolve();
        return DeclaredProperty(typeDef, name);
    }
    
    public static PropertyDefinition? DeclaredProperty(TypeDefinition? typeDef, string name)
    {
        if (typeDef is null)
            return null;
        if (string.IsNullOrEmpty(name))
            return null;
        var propertyDef = typeDef.Properties.FirstOrDefault(x => x.Name == name);
        return propertyDef;
    }

    public static MethodReference? Method(TypeReference? typeRef, string name, TypeReference[]? parameterRefs = null, TypeReference[]? genericsRefs = null)
        => Method(typeRef?.Resolve(), name, parameterRefs, genericsRefs);

    public static MethodReference? Method(TypeDefinition? typeDef, string name, TypeReference[]? parameterRefs = null, TypeReference[]? genericsRefs = null)
    {
        if (typeDef is null)
            return null;
        if (string.IsNullOrEmpty(name))
            return null;
        MethodReference? result;
        if (parameterRefs is null)
        {
            result = FindIncludingBaseTypes(typeDef, t => t.Methods.FirstOrDefault(x =>
            {
                if (x.Name != name)
                    return false;
                return true;
            }));
            if (result is null)
            {
                throw new AmbiguousMatchException($"Ambiguous match in Harmony patch for {typeDef}:{name}");
            }
        }
        else
        {
            result = FindIncludingBaseTypes(typeDef, t => t.Methods.FirstOrDefault(x =>
            {
                if (x.Name != name)
                    return false;
                if (x.Parameters.Count != parameterRefs.Length)
                    return false;
                for (var i = 0; i < parameterRefs.Length; i++)
                {
                    var xParamTypeRef = x.Parameters[i].ParameterType;
                    var paramTypeRef = parameterRefs[i];
                    if (xParamTypeRef.Resolve() != paramTypeRef.Resolve())
                        return false;
                }
                return true;
            }));
        }

        if (result is null)
            return null;

        if (genericsRefs is not null)
        {
            var inst = new GenericInstanceMethod(result);
            foreach (var x in genericsRefs)
            {
                inst.GenericArguments.Add(x);
            }
            result = inst;
        }
        return result;
    }

    public static EventDefinition? DeclaredEvent(TypeReference? typeRef, string name)
    {
        var typeDef = typeRef?.Resolve();
        return DeclaredEvent(typeDef, name);
    }

    public static EventDefinition? DeclaredEvent(TypeDefinition? typeDef, string name)
    {
        if (typeDef is null)
            return null;
        if (string.IsNullOrEmpty(name))
            return null;
        var eventDef = typeDef.Events.FirstOrDefault(x => x.Name == name);
        return eventDef;
    }
    
    public static T? FindIncludingBaseTypes<T>(TypeDefinition typeDef, Func<TypeDefinition, T?> func)
        where T : class
    {
        TypeDefinition? t = typeDef;
        while (t != null)
        {
            var result = func(t);
            if (result != null)
                return result;
            t = t.BaseType?.Resolve();
        }
        return null;
    }
    
    public static FieldDefinition? DeclaredField(TypeDefinition? typeDef, string? name)
    {
        if (typeDef is null)
            return null;
        if (string.IsNullOrEmpty(name))
            return null;
        var fieldDef = typeDef.Fields.FirstOrDefault(x => x.Name == name);
        return fieldDef;
    }
    
    public static FieldDefinition? DeclaredField(TypeDefinition? typeDef, int idx)
    {
        if (typeDef is null)
            return null;
        var fieldDef = GetDeclaredFields(typeDef).ElementAtOrDefault(idx);
        return fieldDef;
    }
    
    public static List<FieldDefinition> GetDeclaredFields(TypeDefinition? type)
    {
        if (type is null)
            return [];
        return [.. type.Fields];
    }
    
    public static FieldDefinition? Field(TypeDefinition? typeDef, string? name)
    {
        if (typeDef is null)
            return null;
        if (string.IsNullOrEmpty(name))
            return null;
        var fieldDef = FindIncludingBaseTypes(typeDef, t => t.Fields.FirstOrDefault(x => x.Name == name));
        return fieldDef;
    }
}