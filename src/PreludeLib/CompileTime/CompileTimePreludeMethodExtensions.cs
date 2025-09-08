using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Mono.Cecil;

namespace PreludeLib.CompileTime;

public static class CompileTimePreludeMethodExtensions
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
            .Where(info => info is not null)];
    }

    public static List<CompileTimePreludeMethod> GetFromTypeRef(TypeReference typeRef)
        => GetFromTypeDef(typeRef.Resolve());
    
    static CompileTimePreludeMethod? GetPreludeMethodInfo(object attribute)
    {
        var f_info = attribute.GetType().GetField(nameof(HarmonyAttribute.info), AccessTools.all);
        if (f_info is null)
            return null;
        if (f_info.FieldType.FullName != CompileTimePatchTools.HarmonyMethodFullName)
            return null;
        var info = f_info.GetValue(attribute);
        return AccessTools.MakeDeepCopy<CompileTimePreludeMethod>(info);
    }
}