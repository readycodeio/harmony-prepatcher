using System.Reflection;
using HarmonyLib;
using Mono.Cecil;
using PreludeLib.CompileTime.Public;

namespace PreludeLib.CompileTime.Utils;

public static class CompileTimePreludeMethodUtils
{
    internal static void SetValue(Traverse trv, string name, object? val)
    {
        if (val is null)
            return;
        var fld = trv.Field(name);
        if (name == nameof(CompileTimePreludeMethod.MethodType) || name == nameof(CompileTimePreludeMethod.ReversePatchType))
        {
            var enumType = Nullable.GetUnderlyingType(fld.GetValueType())!;
            val = Enum.ToObject(enumType, (int)val);
        }
        _ = fld.SetValue(val);
    }
    
    public static CompileTimePreludeMethod Merge(this CompileTimePreludeMethod master, CompileTimePreludeMethod? detail)
    {
        if (detail is null)
            return master;
        var result = new CompileTimePreludeMethod();
        var resultTrv = Traverse.Create(result);
        var masterTrv = Traverse.Create(master);
        var detailTrv = Traverse.Create(detail);
        CompileTimePreludeMethod.HarmonyFields().ForEach(f =>
        {
            var baseValue = masterTrv.Field(f).GetValue();
            var detailValue = detailTrv.Field(f).GetValue();
            if (f != nameof(CompileTimePreludeMethod.Priority))
                SetValue(resultTrv, f, detailValue ?? baseValue);
            else
            {
                // This if is needed because priority defaults to -1
                // This causes the value of a HarmonyPriority attribute to be overriden by the next attribute if it is not merged last
                // should be removed by making priority nullable and default to null at some point

                var baseInt = (int)baseValue;
                var detailInt = (int)detailValue;
                var priority = Math.Max(baseInt, detailInt);
                if (baseInt == -1 && detailInt != -1)
                    priority = detailInt;
                if (baseInt != -1 && detailInt == -1)
                    priority = baseInt;
                SetValue(resultTrv, f, priority);
            }
        });
        return result;
    }
	
    public static List<CompileTimePreludeMethod> GetFromTypeDef(TypeDefinition typeDef)
    {
        var allAttributes = typeDef.CustomAttributes.ToList();
        var t = typeDef;
        while (t != null)
        {
            allAttributes.AddRange(t.CustomAttributes);
            t = t.BaseType.Resolve();
        }
        
        return [.. allAttributes
            .Select(GetPreludeMethodInfo)
            .Where(info => info is not null)!];
    }

    public static List<CompileTimePreludeMethod> GetFromTypeRef(TypeReference typeRef)
        => GetFromTypeDef(typeRef.Resolve());
    
    static CompileTimePreludeMethod? GetPreludeMethodInfo(object attribute)
    {
        var f_info = attribute.GetType().GetField(nameof(HarmonyAttribute.info), AccessTools.all);
        if (f_info is null)
            return null;
        if (f_info.FieldType.FullName != nameof(HarmonyMethod))
            return null;
        var info = f_info.GetValue(attribute);
        return AccessTools.MakeDeepCopy<CompileTimePreludeMethod>(info);
    }
    
	public static MethodReference? GetOriginalMethod(this CompileTimePreludeMethod patchMethod)
	{
		try
		{
			switch (patchMethod.MethodType)
			{
				case MethodType.Normal:
					if (string.IsNullOrEmpty(patchMethod.MethodName))
						return null;
					return CompileTimeAccessTools.DeclaredMethod(patchMethod.DeclaringType, patchMethod.MethodName, patchMethod.ArgumentTypes);

				case MethodType.Getter:
					if (string.IsNullOrEmpty(patchMethod.MethodName))
						return CompileTimeAccessTools.DeclaredIndexerGetter(patchMethod.DeclaringType, patchMethod.ArgumentTypes);
					return CompileTimeAccessTools.DeclaredPropertyGetter(patchMethod.DeclaringType, patchMethod.MethodName);

				case MethodType.Setter:
					if (string.IsNullOrEmpty(patchMethod.MethodName))
						return CompileTimeAccessTools.DeclaredIndexerSetter(patchMethod.DeclaringType, patchMethod.ArgumentTypes);
					return CompileTimeAccessTools.DeclaredPropertySetter(patchMethod.DeclaringType, patchMethod.MethodName);

				case MethodType.Constructor:
					return CompileTimeAccessTools.DeclaredConstructor(patchMethod.DeclaringType, patchMethod.ArgumentTypes);

				case MethodType.StaticConstructor:
					return CompileTimeAccessTools.GetDeclaredConstructors(patchMethod.DeclaringType)
						.FirstOrDefault(c => c.IsStatic);

				case MethodType.Enumerator:
					if (string.IsNullOrEmpty(patchMethod.MethodName))
						return null;
					var enumMethod = CompileTimeAccessTools.DeclaredMethod(patchMethod.DeclaringType, patchMethod.MethodName, patchMethod.ArgumentTypes);
					return CompileTimeAccessTools.EnumeratorMoveNext(enumMethod);

				case MethodType.Async:
					if (string.IsNullOrEmpty(patchMethod.MethodName))
						return null;
					var asyncMethod = CompileTimeAccessTools.DeclaredMethod(patchMethod.DeclaringType, patchMethod.MethodName, patchMethod.ArgumentTypes);
					return CompileTimeAccessTools.AsyncMoveNext(asyncMethod);

				case MethodType.Finalizer:
					return CompileTimeAccessTools.DeclaredFinalizer(patchMethod.DeclaringType);

				case MethodType.EventAdd:
					if (string.IsNullOrEmpty(patchMethod.MethodName))
						return null;
					return CompileTimeAccessTools.DeclaredEventAdder(patchMethod.DeclaringType, patchMethod.MethodName);

				case MethodType.EventRemove:
					if (string.IsNullOrEmpty(patchMethod.MethodName))
						return null;
					return CompileTimeAccessTools.DeclaredEventRemover(patchMethod.DeclaringType, patchMethod.MethodName);

				case MethodType.OperatorImplicit:
				case MethodType.OperatorExplicit:
				case MethodType.OperatorUnaryPlus:
				case MethodType.OperatorUnaryNegation:
				case MethodType.OperatorLogicalNot:
				case MethodType.OperatorOnesComplement:
				case MethodType.OperatorIncrement:
				case MethodType.OperatorDecrement:
				case MethodType.OperatorTrue:
				case MethodType.OperatorFalse:
				case MethodType.OperatorAddition:
				case MethodType.OperatorSubtraction:
				case MethodType.OperatorMultiply:
				case MethodType.OperatorDivision:
				case MethodType.OperatorModulus:
				case MethodType.OperatorBitwiseAnd:
				case MethodType.OperatorBitwiseOr:
				case MethodType.OperatorExclusiveOr:
				case MethodType.OperatorLeftShift:
				case MethodType.OperatorRightShift:
				case MethodType.OperatorEquality:
				case MethodType.OperatorInequality:
				case MethodType.OperatorGreaterThan:
				case MethodType.OperatorLessThan:
				case MethodType.OperatorGreaterThanOrEqual:
				case MethodType.OperatorLessThanOrEqual:
				case MethodType.OperatorComma:
					var methodName = "op_" + (patchMethod.MethodType?.ToString().Replace("Operator", "") ?? "unknown");
					return CompileTimeAccessTools.DeclaredMethod(patchMethod.DeclaringType, methodName, patchMethod.ArgumentTypes);
			}
		}
		catch (AmbiguousMatchException ex)
		{
			throw new HarmonyException($"Ambiguous match for HarmonyMethod[{patchMethod.Description()}]", ex.InnerException ?? ex);
		}

		return null;
	}
}