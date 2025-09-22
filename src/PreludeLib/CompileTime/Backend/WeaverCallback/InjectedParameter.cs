extern alias OfficialCecil;
using HarmonyLib;
using OfficialCecil::Mono.Cecil;
using PreludeLib.CompileTime.Utils;

namespace PreludeLib.CompileTime.Backend.WeaverCallback;

public class InjectedParameter
{
    public ParameterDefinition ParameterDef;
    public string RealName;
    public InjectionType InjectionType;

    public const string INSTANCE_PARAM = "__instance";
    public const string ORIGINAL_METHOD_PARAM = "__originalMethod";
    public const string ARGS_ARRAY_VAR = "__args";
    public const string RESULT_VAR = "__result";
    public const string RESULT_REF_VAR = "__resultRef";
    public const string STATE_VAR = "__state";
    public const string EXCEPTION_VAR = "__exception";
    public const string RUN_ORIGINAL_VAR = "__runOriginal";

    public InjectedParameter(MethodDefinition methodDef, ParameterDefinition parameterDef)
    {
        ParameterDef = parameterDef;
        RealName = CalculateRealName(methodDef);
        InjectionType = Type(RealName);
    }

    private string CalculateRealName(MethodDefinition methodDef)
    {
        var baseArgs = CompileTimePreludeCecilUtils.GetArgumentAttributes(methodDef);
        if (methodDef.DeclaringType is not null)
            baseArgs = baseArgs.Union(CompileTimePreludeCecilUtils.GetArgumentAttributes(methodDef.DeclaringType));
        var arg = CompileTimePreludeCecilUtils.GetArgumentAttribute(ParameterDef);
        if (arg != null)
            return arg.OriginalName ?? ParameterDef.Name;
        return baseArgs.GetRealName(ParameterDef.Name, null) ?? ParameterDef.Name;
    }

    private static readonly Dictionary<string, InjectionType> Types = new()
    {
        { INSTANCE_PARAM, InjectionType.Instance },
        { ORIGINAL_METHOD_PARAM, InjectionType.OriginalMethod },
        { ARGS_ARRAY_VAR, InjectionType.ArgsArray },
        { RESULT_VAR, InjectionType.Result },
        { RESULT_REF_VAR, InjectionType.ResultRef },
        { STATE_VAR, InjectionType.State },
        { EXCEPTION_VAR, InjectionType.Exception },
        { RUN_ORIGINAL_VAR, InjectionType.RunOriginal },
    };

    private static InjectionType Type(string name)
    {
        if (Types.TryGetValue(name, out var type))
            return type;
        return InjectionType.Unknown;
    }
}