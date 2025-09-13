using HarmonyLib;
using Mono.Cecil;
using PreludeLib.CompileTime.Utils;

namespace PreludeLib.CompileTime.Public;

public class CompileTimePreludeMethod
{
	public MethodReference? Method; // need to be called 'method'
	public readonly string? Category = null;
	public readonly TypeReference? DeclaringType;
	public readonly string? MethodName;
	public MethodType? MethodType;
	public readonly TypeReference[]? ArgumentTypes;
	public int Priority;
	public string[]? Before;
	public string[]? After;
	public HarmonyReversePatchType? ReversePatchType;
	public bool? Debug;
	public bool NonVirtualDelegate;

	public static List<string> HarmonyFields()
	{
		return [.. AccessTools.GetFieldNames(typeof(CompileTimePreludeMethod))];
	}
	
	public CompileTimePreludeMethod()
	{
		// empty
	}
	
	public CompileTimePreludeMethod(ModuleDefinition moduleDef, HarmonyMethod harmonyMethod)
	{
		Method = moduleDef.ImportReference(harmonyMethod.method);
		Category = harmonyMethod.category;
		DeclaringType = moduleDef.ImportReference(harmonyMethod.declaringType);
		MethodName = harmonyMethod.methodName;
		MethodType = harmonyMethod.methodType;
		ArgumentTypes = harmonyMethod.argumentTypes?.Select(moduleDef.ImportReference).ToArray() ?? [];
		Priority = harmonyMethod.priority;
		Before = harmonyMethod.before ?? [];
		After = harmonyMethod.after ?? [];
		ReversePatchType = harmonyMethod.reversePatchType;
		Debug = harmonyMethod.debug;
		NonVirtualDelegate = harmonyMethod.nonVirtualDelegate;
	}
	
	public string Description()
	{
		var cName = DeclaringType is not null ? DeclaringType.FullName : "undefined";
		var mName = MethodName ?? "undefined";
		var tName = MethodType.HasValue ? MethodType.Value.ToString() : "undefined";
		var aName = ArgumentTypes is not null ? ArgumentTypes.Description() : "undefined";
		return $"(class={cName}, methodname={mName}, type={tName}, args={aName})";
	}
	
	public static CompileTimePreludeMethod Merge(List<CompileTimePreludeMethod>? attributes)
	{
		var result = new CompileTimePreludeMethod();
		if (attributes is null || attributes.Count == 0)
			return result;
		var resultTrv = Traverse.Create(result);
		attributes.ForEach(attribute =>
		{
			var trv = Traverse.Create(attribute);
			HarmonyFields().ForEach(f =>
			{
				var val = trv.Field(f).GetValue();
				// The second half of this if is needed because priority defaults to -1
				// This causes the value of a HarmonyPriority attribute to be overriden by the next attribute if it is not merged last
				// should be removed by making priority nullable and default to null at some point
				if (val is not null && (f != nameof(Priority) || (int)val != -1))
					CompileTimePreludeMethodExtensions.SetValue(resultTrv, f, val);
			});
		});
		return result;
	}
}