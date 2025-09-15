using HarmonyLib;
using Microsoft.Extensions.Logging;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using PreludeLib.CompileTime.Public;
using PreludeLib.CompileTime.Registry;
using PreludeLib.CompileTime.Utils;

namespace PreludeLib.CompileTime.Backend.WeaverCallback;

public class CompileTimeWeaverBackend(ILogger logger) : ICompileTimeBackend
{
	internal const string PARAM_INDEX_PREFIX = "__";
	const string INSTANCE_FIELD_PREFIX = "___";

	private static readonly Dictionary<OpCode, OpCode> _shortJumps = new()
	{
		{ OpCodes.Leave_S, OpCodes.Leave },
		{ OpCodes.Brfalse_S, OpCodes.Brfalse },
		{ OpCodes.Brtrue_S, OpCodes.Brtrue },
		{ OpCodes.Beq_S, OpCodes.Beq },
		{ OpCodes.Bge_S, OpCodes.Bge },
		{ OpCodes.Bgt_S, OpCodes.Bgt },
		{ OpCodes.Ble_S, OpCodes.Ble },
		{ OpCodes.Blt_S, OpCodes.Blt },
		{ OpCodes.Bne_Un_S, OpCodes.Bne_Un },
		{ OpCodes.Bge_Un_S, OpCodes.Bge_Un },
		{ OpCodes.Bgt_Un_S, OpCodes.Bgt_Un },
		{ OpCodes.Ble_Un_S, OpCodes.Ble_Un },
		{ OpCodes.Br_S, OpCodes.Br },
		{ OpCodes.Blt_Un_S, OpCodes.Blt_Un }
	};
	
    private readonly Dictionary<MethodDefinition, List<Instruction>> _originalInstructions = [];
    
    private void DoPatch(
        MethodDefinition originalDef, 
        IEnumerable<CompileTimePreludeMethod> prefixes,
        IEnumerable<CompileTimePreludeMethod> postfixes,
        IEnumerable<CompileTimePreludeMethod> finalizers)
    {
        var prefixInstr = GenerateInstructions(originalDef, prefixes);
        var postfixInstr = GenerateInstructions(originalDef, postfixes);
        var finalizerInstr = GenerateInstructions(originalDef, finalizers);
        
        EnsurePatchEntry(originalDef, patchMethod.method, out var patchEntry);
        patchEntry.Event.AddEventHandler(null, patchEntry.Del);
    }
    
    internal void UpdateWrapper(MethodDefinition originalDef, PatchInfo patchInfo)
    {
        var debug = patchInfo.Debugging || Harmony.DEBUG;

        var sortedPrefixes = GetSortedPatchMethods(originalDef, patchInfo.prefixes, debug);
        var sortedPostfixes = GetSortedPatchMethods(originalDef, patchInfo.postfixes, debug);
        var sortedTranspilers = GetSortedPatchMethods(originalDef, patchInfo.transpilers, debug);
        var sortedFinalizers = GetSortedPatchMethods(originalDef, patchInfo.finalizers, debug);
        var sortedInnerPrefixes = GetInfixes(patchInfo.innerprefixes);
        var sortedInnerPostfixes = GetInfixes(patchInfo.innerpostfixes);

        var patcher = new MethodCreator(new MethodCreatorConfig(
            originalDef,
            null,
            sortedPrefixes,
            sortedPostfixes,
            sortedTranspilers,
            sortedFinalizers,
            sortedInnerPrefixes,
            sortedInnerPostfixes,
            debug
        ));
        var (replacement, finalInstructions) = patcher.CreateReplacement();
        if (replacement is null) throw new MissingMethodException($"Cannot create replacement for {originalDef.FullDescription()}");

        try
        {
            PatchTools.DetourMethod(originalDef, replacement);
        }
        catch (Exception ex)
        {
            throw HarmonyException.Create(ex, finalInstructions);
        }
        return replacement;
    }
    
    private bool AnyFixHas(Dictionary<CompileTimePreludeMethod, List<InjectedParameter>> injections, InjectionType type)
	    => injections.Values.SelectMany(list => list).Any(pair => pair.InjectionType == type);
    
    private IEnumerable<InjectedParameter> InjectionsFor(Dictionary<CompileTimePreludeMethod, List<InjectedParameter>> injections, CompileTimePreludeMethod fix, InjectionType type = InjectionType.Unknown)
    {
	    if (injections.TryGetValue(fix, out var list))
	    {
		    if (type != InjectionType.Unknown)
			    return list.Where(pair => pair.InjectionType == type);
		    return list;
	    }
	    return [];
    }

    internal void CreateReplacement(MethodDefinition originalDef,
	    List<CompileTimePreludeMethod> prefixes,
	    List<CompileTimePreludeMethod> postfixes,
	    List<CompileTimePreludeMethod> finalizers)
    {
	    var body = originalDef.Body;
	    body.SimplifyMacros();
	    body.InitLocals = true;

	    var il = body.GetILProcessor();
	    var module = originalDef.Module;
	    var ts = module.TypeSystem;

	    HashSet<TypeReference> primitivesWithObjectTypeCode = [ts.IntPtr, ts.UIntPtr];
	    var dateTimeType = module.ImportReference(typeof(DateTime));
	    var decimalType = module.ImportReference(typeof(decimal));
	    var emptyType = module.ImportReference(typeof(void));
	    var dbNullType = module.ImportReference(typeof(DBNull));

	    var fixes = prefixes.Concat(postfixes).Concat(finalizers).ToList();
	    var injections = fixes.ToDictionary(
		    fix => fix,
		    fix => fix.Method!.Parameters.Select(p => new InjectedParameter(fix.Method.Resolve(), p)).ToList()
	    );

	    VariableDefinition? resultVariable = null;

	    var instructions = new List<Instruction>();
	    Dictionary<InjectionType, VariableDefinition> injectedLocals = [];
	    Dictionary<string, VariableDefinition> otherLocals = [];

	    Dictionary<Instruction, List<Instruction>> labels = [];
	    Dictionary<Instruction, List<ExceptionBlock>> blocks = [];

	    if (fixes.Any() && !EqualTypeRef(originalDef.ReturnType, ts.Void))
	    {
		    resultVariable = new VariableDefinition(originalDef.ReturnType);
		    body.Variables.Add(resultVariable);
		    injectedLocals.Add(InjectionType.Result, resultVariable);
		    instructions.AddRange(GenerateVariableInit(resultVariable, true));
	    }

	    if (AnyFixHas(injections, InjectionType.ResultRef))
	    {
		    if (originalDef.ReturnType.IsByReference)
		    {
			    var varType = module.ImportReference(typeof(RefResult<>)).MakeGenericInstanceType(originalDef.ReturnType.GetElementType());
			    var resultRefVariable = new VariableDefinition(varType);
			    body.Variables.Add(resultRefVariable);
			    injectedLocals.Add(InjectionType.ResultRef, resultRefVariable);
			    instructions.Add(Instruction.Create(OpCodes.Ldnull));
			    instructions.Add(Instruction.Create(OpCodes.Stloc, resultRefVariable));
		    }
	    }

	    if (AnyFixHas(injections, InjectionType.ArgsArray))
	    {
		    var argsArrayVariable = new VariableDefinition(module.ImportReference(typeof(object[])));
		    body.Variables.Add(argsArrayVariable);
		    injectedLocals.Add(InjectionType.ArgsArray, argsArrayVariable);
		    instructions.AddRange(PrepareArgumentArray());
		    instructions.Add(Instruction.Create(OpCodes.Stloc, argsArrayVariable));
	    }

	    Instruction? skipOriginalLabel = null;
	    VariableDefinition? runOriginalVariable = null;

	    var prefixAffectsOriginal = prefixes.Any(this.AffectsOriginal);
	    var anyFixHasRunOriginal = AnyFixHas(injections, InjectionType.RunOriginal);
	    if (prefixAffectsOriginal || anyFixHasRunOriginal)
	    {
		    runOriginalVariable = new VariableDefinition(module.ImportReference(typeof(bool)));
		    body.Variables.Add(runOriginalVariable);
		    instructions.Add(Instruction.Create(OpCodes.Ldc_I4_1));
		    instructions.Add(Instruction.Create(OpCodes.Stloc, runOriginalVariable));
		    if (prefixAffectsOriginal)
			    skipOriginalLabel = Instruction.Create(OpCodes.Nop);
	    }

	    fixes.ForEach(fix =>
	    {
		    var declaringType = fix.DeclaringType;
		    if (declaringType is null)
			    return;
		    var varName = declaringType.AssemblyQualifiedName;
		    _ = otherLocals.TryGetValue(varName, out var maybeLocal);
		    foreach (var injection in InjectionsFor(injections, fix, InjectionType.State))
		    {
			    var parameterType = injection.ParameterDef.ParameterType;
			    var type = parameterType.IsByReference ? parameterType.GetElementType() : parameterType;
			    if (maybeLocal != null)
				    continue;
			    var privateStateVariable = new VariableDefinition(type);
			    body.Variables.Add(privateStateVariable);
			    otherLocals.Add(varName, privateStateVariable);
			    instructions.AddRange(GenerateVariableInit(privateStateVariable));
		    }
	    });

	    VariableDefinition? finalizedVariable = null;
	    VariableDefinition? exceptionVariable = null;
	    if (finalizers.Count > 0)
	    {
		    finalizedVariable = new VariableDefinition(module.ImportReference(typeof(bool)));
		    body.Variables.Add(finalizedVariable);
		    instructions.AddRange(GenerateVariableInit(finalizedVariable));
		    exceptionVariable = new VariableDefinition(module.ImportReference(typeof(Exception)));
		    body.Variables.Add(exceptionVariable);
		    injectedLocals.Add(InjectionType.Exception, exceptionVariable);
		    instructions.AddRange(GenerateVariableInit(exceptionVariable));
		    // begin try
		    instructions.Add(MarkBlock(ExceptionBlockType.BeginExceptionBlock));
	    }

	    AddPrefixes();
	    if (skipOriginalLabel != null)
	    {
		    instructions.Add(Instruction.Create(OpCodes.Ldloc, runOriginalVariable));
		    instructions.Add(Instruction.Create(OpCodes.Brfalse, skipOriginalLabel));
	    }

	    var endLabels = new List<Instruction>();

	    instructions.Add(Instruction.Create(OpCodes.Nop, "start original"));
	    instructions.AddRange(CleanupCodes(endLabels));
	    instructions.Add(Instruction.Create(OpCodes.Nop, "end original"));

	    if (endLabels.Count > 0)
		    instructions.Add(NopWithLabelList(endLabels));
	    if (resultVariable is not null)
		    instructions.Add(Instruction.Create(OpCodes.Stloc, resultVariable));
	    if (skipOriginalLabel != null)
		    instructions.Add(NopWithLabels(skipOriginalLabel));

	    _ = AddPostfixes(false);
	    if (resultVariable is not null)
		    instructions.Add(Instruction.Create(OpCodes.Ldloc, resultVariable));

	    var needsToStorePassthroughResult = AddPostfixes(true);

	    if (finalizers.Count > 0)
	    {
		    exceptionVariable = injectedLocals[InjectionType.Exception];

		    if (needsToStorePassthroughResult)
		    {
			    instructions.Add(Instruction.Create(OpCodes.Stloc, resultVariable));
			    instructions.Add(Instruction.Create(OpCodes.Ldloc, resultVariable));
		    }

		    _ = AddFinalizers(false);
		    instructions.Add(Instruction.Create(OpCodes.Ldc_I4_1));
		    instructions.Add(Instruction.Create(OpCodes.Stloc, finalizedVariable));
		    var noExceptionLabel1 = Instruction.Create(OpCodes.Nop);
		    instructions.Add(Instruction.Create(OpCodes.Ldloc, exceptionVariable));
		    instructions.Add(Instruction.Create(OpCodes.Brfalse, noExceptionLabel1));
		    instructions.Add(Instruction.Create(OpCodes.Ldloc, exceptionVariable));
		    instructions.Add(Instruction.Create(OpCodes.Throw));
		    instructions.Add(NopWithLabels(noExceptionLabel1));

		    // end try, begin catch
		    instructions.Add(MarkBlock(ExceptionBlockType.BeginCatchBlock));
		    instructions.Add(Instruction.Create(OpCodes.Stloc, exceptionVariable));

		    instructions.Add(Instruction.Create(OpCodes.Ldloc, finalizedVariable));
		    var endFinalizerLabel = Instruction.Create(OpCodes.Nop);
		    instructions.Add(Instruction.Create(OpCodes.Brtrue, endFinalizerLabel));

		    var rethrowPossible = AddFinalizers(true);

		    instructions.Add(NopWithLabels(endFinalizerLabel));

		    var noExceptionLabel2 = Instruction.Create(OpCodes.Nop);
		    instructions.Add(Instruction.Create(OpCodes.Ldloc, exceptionVariable));
		    instructions.Add(Instruction.Create(OpCodes.Brfalse, noExceptionLabel2));
		    if (rethrowPossible)
			    instructions.Add(Instruction.Create(OpCodes.Rethrow));
		    else
		    {
			    instructions.Add(Instruction.Create(OpCodes.Ldloc, exceptionVariable));
			    instructions.Add(Instruction.Create(OpCodes.Throw));
		    }
		    instructions.Add(NopWithLabels(noExceptionLabel2));

		    // end catch
		    instructions.Add(MarkBlock(ExceptionBlockType.EndExceptionBlock));

		    if (resultVariable is not null)
			    instructions.Add(Instruction.Create(OpCodes.Ldloc, resultVariable));
	    }

	    if (skipOriginalLabel is not null || finalizers.Count > 0 || postfixes.Count > 0)
		    instructions.Add(Instruction.Create(OpCodes.Ret));

	    instructions = FaultRewrite(instructions);

	    var codeEmitter = new Emitter(config.il);
	    this.EmitCodes(codeEmitter, config.instructions);
	    var replacementMethod = config.patch.Generate();

	    return (replacementMethod, codeEmitter.GetInstructions());

	    IEnumerable<Instruction> CleanupCodes(List<Instruction> outEndLabels)
	    {
		    foreach (var instruction in body.Instructions)
		    {
			    var code = instruction.OpCode;
			    if (code == OpCodes.Ret)
			    {
				    var endLabel = Instruction.Create(OpCodes.Nop);
				    var br = Instruction.Create(OpCodes.Br, endLabel);
				    if (labels.TryGetValue(instruction, out var instrLabels))
					    labels.Add(br, [..instrLabels]);
				    if (blocks.TryGetValue(instruction, out var instrBlocks))
					    blocks.Add(br, [..instrBlocks]);
				    yield return br;
				    outEndLabels.Add(endLabel);
			    }
			    else if (_shortJumps.TryGetValue(code, out var longJump))
			    {
				    var newInstr = Instruction.Create(longJump);
				    newInstr.Operand = instruction.Operand;
				    if (labels.TryGetValue(instruction, out var instrLabels))
					    labels.Add(newInstr, [..instrLabels]);
				    if (blocks.TryGetValue(instruction, out var instrBlocks))
					    blocks.Add(newInstr, [..instrBlocks]);
				    yield return newInstr;
			    }
			    else
				    yield return instruction;
		    }
	    }

	    Instruction MarkBlock(ExceptionBlockType blockType)
	    {
		    var instr = Instruction.Create(OpCodes.Nop);
		    blocks.Add(instr, [new ExceptionBlock(blockType)]);
		    return instr;
	    }

	    void AddPrefixes()
	    {
		    foreach (var fix in prefixes)
		    {
			    var skipLabel = this.AffectsOriginal(fix) ? Instruction.Create(OpCodes.Nop) : null;
			    if (skipLabel != null)
			    {
				    instructions.Add(Instruction.Create(OpCodes.Ldloc, runOriginalVariable));
				    instructions.Add(Instruction.Create(OpCodes.Brfalse, skipLabel));
			    }

			    var tmpBoxVars = new List<KeyValuePair<VariableDefinition, TypeReference>>();
			    instructions.AddRange(EmitCallParameter(fix, false, out var tmpInstanceBoxingVar, out var tmpObjectVar, out var refResultUsed, tmpBoxVars));
			    instructions.Add(Instruction.Create(OpCodes.Call, fix.Method));
			    if (OriginalParameters(fix.Method!).Any(pair => pair.realName == MethodPatcherTools.ARGS_ARRAY_VAR))
				    instructions.AddRange(RestoreArgumentArray());
			    if (tmpInstanceBoxingVar != null)
			    {
				    instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
				    instructions.Add(Instruction.Create(OpCodes.Ldloc, tmpInstanceBoxingVar));
				    instructions.Add(Instruction.Create(OpCodes.Unbox_Any, originalDef.DeclaringType));
				    instructions.Add(Instruction.Create(OpCodes.Stobj, originalDef.DeclaringType));
			    }

			    if (refResultUsed)
			    {
				    var label = Instruction.Create(OpCodes.Nop);
				    instructions.Add(Instruction.Create(OpCodes.Ldloc, injectedLocals[InjectionType.ResultRef]));
				    instructions.Add(Instruction.Create(OpCodes.Brfalse_S, label));

				    instructions.Add(Instruction.Create(OpCodes.Ldloc, injectedLocals[InjectionType.ResultRef]));
				    instructions.Add(Instruction.Create(OpCodes.Callvirt, CompileTimeAccessTools.Method(injectedLocals[InjectionType.ResultRef].VariableType, "Invoke")));
				    instructions.Add(Instruction.Create(OpCodes.Stloc, injectedLocals[InjectionType.Result]));
				    instructions.Add(Instruction.Create(OpCodes.Ldnull));
				    instructions.Add(Instruction.Create(OpCodes.Stloc, injectedLocals[InjectionType.ResultRef]));

				    var instr = Instruction.Create(OpCodes.Nop);
				    labels.Add(instr, [label]);
				    instructions.Add(instr);
			    }
			    else if (tmpObjectVar != null)
			    {
				    instructions.Add(Instruction.Create(OpCodes.Ldloc, tmpObjectVar));
				    var originalReturnType = originalDef.ReturnType ?? ts.Void;
				    instructions.Add(Instruction.Create(OpCodes.Unbox_Any, originalReturnType));
				    instructions.Add(Instruction.Create(OpCodes.Stloc, injectedLocals[InjectionType.Result]));
			    }

			    tmpBoxVars.Do(tmpBoxVar =>
			    {
				    instructions.Add(Instruction.Create(originalDef.IsStatic ? OpCodes.Ldarg_0 : OpCodes.Ldarg_1));
				    instructions.Add(Instruction.Create(OpCodes.Ldloc, tmpBoxVar.Key));
				    instructions.Add(Instruction.Create(OpCodes.Unbox_Any, tmpBoxVar.Value));
				    instructions.Add(Instruction.Create(OpCodes.Stobj, tmpBoxVar.Value));
			    });

			    var returnType = fix.Method!.ReturnType ?? ts.Void;
			    if (!EqualTypeRef(returnType, ts.Void))
			    {
				    if (!EqualTypeRef(returnType, ts.Boolean))
					    throw new Exception(
						    $"Prefix patch {fix} has not \"bool\" or \"void\" return type: {returnType}");
				    instructions.Add(Instruction.Create(OpCodes.Stloc, runOriginalVariable));
			    }

			    if (skipLabel != null)
			    {
				    var instr = Instruction.Create(OpCodes.Nop);
				    labels.Add(instr, [skipLabel]);
				    instructions.Add(instr);
			    }
		    }
	    }

	    bool AddPostfixes(bool passthroughPatches)
	    {
		    var result = false;
		    var originalIsStatic = originalDef.IsStatic;
		    foreach (var fix in postfixes.Where(fix => passthroughPatches == !EqualTypeRef(fix.Method!.ReturnType, ts.Void)))
		    {
			    var tmpBoxVars = new List<KeyValuePair<VariableDefinition, TypeReference>>();
			    instructions.AddRange(EmitCallParameter(fix, true, out var tmpInstanceBoxingVar, out var tmpObjectVar, out var refResultUsed, tmpBoxVars));
			    instructions.Add(Instruction.Create(OpCodes.Call, fix.Method));
			    if (OriginalParameters(fix.Method!).Any(pair => pair.realName == MethodPatcherTools.ARGS_ARRAY_VAR))
				    instructions.AddRange(RestoreArgumentArray());
			    if (tmpInstanceBoxingVar != null)
			    {
				    instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
				    instructions.Add(Instruction.Create(OpCodes.Ldloc, tmpInstanceBoxingVar));
				    instructions.Add(Instruction.Create(OpCodes.Unbox_Any, originalDef.DeclaringType));
				    instructions.Add(Instruction.Create(OpCodes.Stobj, originalDef.DeclaringType));
			    }

			    if (refResultUsed)
			    {
				    var label = Instruction.Create(OpCodes.Nop);
				    instructions.Add(Instruction.Create(OpCodes.Ldloc, injectedLocals[InjectionType.ResultRef]));
				    instructions.Add(Instruction.Create(OpCodes.Brfalse_S, label));

				    instructions.Add(Instruction.Create(OpCodes.Ldloc, injectedLocals[InjectionType.ResultRef]));
				    instructions.Add(Instruction.Create(OpCodes.Callvirt, CompileTimeAccessTools.Method(injectedLocals[InjectionType.ResultRef].VariableType, "Invoke")));
				    instructions.Add(Instruction.Create(OpCodes.Stloc, injectedLocals[InjectionType.Result]));
				    instructions.Add(Instruction.Create(OpCodes.Ldnull));
				    instructions.Add(Instruction.Create(OpCodes.Stloc, injectedLocals[InjectionType.ResultRef]));

					var instr = Instruction.Create(OpCodes.Nop);
					labels.Add(instr, [label]);
				    instructions.Add(instr);
			    }
			    else if (tmpObjectVar != null)
			    {
				    instructions.Add(Instruction.Create(OpCodes.Ldloc, tmpObjectVar));
				    var originalReturnType = originalDef.ReturnType ?? ts.Void;
				    instructions.Add(Instruction.Create(OpCodes.Unbox_Any, originalReturnType));
				    instructions.Add(Instruction.Create(OpCodes.Stloc, injectedLocals[InjectionType.Result]));
			    }

			    tmpBoxVars.Do(tmpBoxVar =>
			    {
				    instructions.Add(Instruction.Create(originalIsStatic ? OpCodes.Ldarg_0 : OpCodes.Ldarg_1));
				    instructions.Add(Instruction.Create(OpCodes.Ldloc, tmpBoxVar.Key));
				    instructions.Add(Instruction.Create(OpCodes.Unbox_Any, tmpBoxVar.Value));
				    instructions.Add(Instruction.Create(OpCodes.Stobj, tmpBoxVar.Value));
			    });

			    if (!EqualTypeRef(fix.Method!.ReturnType, ts.Void))
			    {
				    var firstFixParam = fix.Method.Parameters.FirstOrDefault();
				    var hasPassThroughResultParam =
					    firstFixParam is not null && EqualTypeRef(fix.Method.ReturnType, firstFixParam.ParameterType);
				    if (hasPassThroughResultParam)
					    result = true;
				    else
				    {
					    if (firstFixParam is not null)
						    throw new Exception(
							    $"Return type of pass through postfix {fix} does not match type of its first parameter");

					    throw new Exception($"Postfix patch {fix} must have a \"void\" return type");
				    }
			    }
		    }

		    return result;
	    }

	    bool AddFinalizers(bool catchExceptions)
	    {
		    var rethrowPossible = true;
		    var originalIsStatic = originalDef.IsStatic;
		    finalizers.Do(fix =>
		    {
			    if (catchExceptions)
			    {
				    
				    instructions.Add(MarkBlock(ExceptionBlockType.BeginExceptionBlock));
			    }

			    var tmpBoxVars = new List<KeyValuePair<VariableDefinition, TypeReference>>();
			    instructions.AddRange(EmitCallParameter(fix, false, out var tmpInstanceBoxingVar, out var tmpObjectVar, out var refResultUsed, tmpBoxVars));
			    instructions.Add(Instruction.Create(OpCodes.Call, fix.Method!));
			    if (OriginalParameters(fix.Method!).Any(pair => pair.realName == MethodPatcherTools.ARGS_ARRAY_VAR))
				    instructions.AddRange(RestoreArgumentArray());
			    if (tmpInstanceBoxingVar != null)
			    {
				    instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
				    instructions.Add(Instruction.Create(OpCodes.Ldloc, tmpInstanceBoxingVar));
				    instructions.Add(Instruction.Create(OpCodes.Unbox_Any, originalDef.DeclaringType));
				    instructions.Add(Instruction.Create(OpCodes.Stobj, originalDef.DeclaringType));
			    }

			    if (refResultUsed)
			    {
				    var label = Instruction.Create(OpCodes.Nop);
				    instructions.Add(Instruction.Create(OpCodes.Ldloc, injectedLocals[InjectionType.ResultRef]));
				    instructions.Add(Instruction.Create(OpCodes.Brfalse_S, label));

				    instructions.Add(Instruction.Create(OpCodes.Ldloc, injectedLocals[InjectionType.ResultRef]));
				    instructions.Add(Instruction.Create(OpCodes.Callvirt, CompileTimeAccessTools.Method(injectedLocals[InjectionType.ResultRef].VariableType, "Invoke")));
				    instructions.Add(Instruction.Create(OpCodes.Stloc, injectedLocals[InjectionType.Result]));
				    instructions.Add(Instruction.Create(OpCodes.Ldnull));
				    instructions.Add(Instruction.Create(OpCodes.Stloc, injectedLocals[InjectionType.ResultRef]));

				    var instr = Instruction.Create(OpCodes.Nop);
				    labels.Add(instr, [label]);
				    instructions.Add(instr);
			    }
			    else if (tmpObjectVar != null)
			    {
				    instructions.Add(Instruction.Create(OpCodes.Ldloc, tmpObjectVar));
				    instructions.Add(Instruction.Create(OpCodes.Unbox_Any, GetReturnedType(originalDef)));
				    instructions.Add(Instruction.Create(OpCodes.Stloc, injectedLocals[InjectionType.Result]));
			    }

			    tmpBoxVars.Do(tmpBoxVar =>
			    {
				    instructions.Add(Instruction.Create(originalIsStatic ? OpCodes.Ldarg_0 : OpCodes.Ldarg_1));
				    instructions.Add(Instruction.Create(OpCodes.Ldloc, tmpBoxVar.Key));
				    instructions.Add(Instruction.Create(OpCodes.Unbox_Any, tmpBoxVar.Value));
				    instructions.Add(Instruction.Create(OpCodes.Stobj, tmpBoxVar.Value));
			    });

			    if (!EqualTypeRef(fix.Method!.ReturnType, ts.Void))
			    {
				    instructions.Add(Instruction.Create(OpCodes.Stloc, injectedLocals[InjectionType.Exception]));
				    rethrowPossible = false;
			    }

			    if (catchExceptions)
			    {
				    instructions.Add(MarkBlock(ExceptionBlockType.BeginCatchBlock));
				    instructions.Add(Instruction.Create(OpCodes.Pop));
				    instructions.Add(MarkBlock(ExceptionBlockType.EndExceptionBlock));
			    }
		    });

		    return rethrowPossible;
	    }

	    List<Instruction> PrepareArgumentArray()
	    {
		    var result = new List<Instruction>();
		    var originalIsStatic = originalDef.IsStatic;
		    var parameters = originalDef.Parameters;
		    var i = 0;
		    foreach (var pInfo in parameters)
		    {
			    var argIndex = i++ + (originalIsStatic ? 0 : 1);
			    if (pInfo.IsOut || pInfo.Attributes.HasFlag(ParameterAttributes.Retval))
				    result.AddRange(InitializeOutParameter(argIndex, pInfo.ParameterType));
		    }

		    result.Add(Instruction.Create(OpCodes.Ldc_I4, parameters.Count));
		    result.Add(Instruction.Create(OpCodes.Newarr, module.ImportReference(typeof(object))));
		    i = 0;
		    var arrayIdx = 0;
		    foreach (var pInfo in parameters)
		    {
			    var argIndex = i++ + (originalIsStatic ? 0 : 1);
			    var pType = pInfo.ParameterType;
			    var paramByRef = pType.IsByReference;
			    if (paramByRef)
				    pType = pType.GetElementType();
			    result.Add(Instruction.Create(OpCodes.Dup));
			    result.Add(Instruction.Create(OpCodes.Ldc_I4, arrayIdx++));
			    result.Add(Instruction.Create(OpCodes.Ldarg, argIndex));
			    if (paramByRef)
			    {
				    if (IsStruct(pType))
					    result.Add(Instruction.Create(OpCodes.Ldobj, pType));
				    else
					    result.Add(LoadIndOpCodeFor(pType));
			    }

			    if (pType.IsValueType)
				    result.Add(Instruction.Create(OpCodes.Box, pType));
			    result.Add(Instruction.Create(OpCodes.Stelem_Ref));
		    }

		    return result;
	    }

	    bool IsStruct(TypeReference? typeRef)
	    {
		    if (typeRef == null)
			    return false;
		    return typeRef.IsValueType && !IsValue(typeRef) && !IsVoid(typeRef);
	    }

	    bool IsVoid(TypeReference typeRef) => EqualTypeRef(typeRef, ts.Void);

	    bool IsClass(TypeReference? typeRef)
	    {
		    if (typeRef == null)
			    return false;
		    return !typeRef.IsValueType;
	    }

	    bool IsValue(TypeReference? typeRef)
	    {
		    if (typeRef == null)
			    return false;
		    return typeRef.IsPrimitive || typeRef.Resolve().IsEnum;
	    }

	    List<Instruction> GenerateVariableInit(VariableDefinition variableDef, bool isReturnValue = false)
	    {
		    var result = new List<Instruction>();
		    var typeRef = variableDef.VariableType;

		    if (typeRef.IsByReference)
		    {
			    if (isReturnValue)
			    {
				    result.Add(Instruction.Create(OpCodes.Ldc_I4_1));
				    result.Add(Instruction.Create(OpCodes.Newarr, typeRef.GetElementType()));
				    result.Add(Instruction.Create(OpCodes.Ldc_I4_0));
				    result.Add(Instruction.Create(OpCodes.Ldelema, typeRef.GetElementType()));
				    result.Add(Instruction.Create(OpCodes.Stloc, variableDef));
				    return result;
			    }
			    else
				    typeRef = typeRef.GetElementType();
		    }

		    if (typeRef.Resolve().IsEnum)
		    {
			    typeRef = typeRef.GetElementType();
		    }

		    if (IsClass(typeRef))
		    {
			    result.Add(Instruction.Create(OpCodes.Ldnull));
			    result.Add(Instruction.Create(OpCodes.Stloc, variableDef));
			    return result;
		    }

		    if (IsStruct(typeRef))
		    {
			    result.Add(Instruction.Create(OpCodes.Ldloca, variableDef));
			    result.Add(Instruction.Create(OpCodes.Initobj, typeRef));
			    return result;
		    }

		    if (IsValue(typeRef))
		    {
			    if (EqualTypeRef(typeRef, ts.Single))
				    result.Add(Instruction.Create(OpCodes.Ldc_R4, (float)0));
			    else if (EqualTypeRef(typeRef, ts.Double))
				    result.Add(Instruction.Create(OpCodes.Ldc_R8, (double)0));
			    else if (EqualTypeRef(typeRef, ts.Int64) || EqualTypeRef(typeRef, ts.UInt64))
				    result.Add(Instruction.Create(OpCodes.Ldc_I8, (long)0));
			    else
				    result.Add(Instruction.Create(OpCodes.Ldc_I4, 0));
			    result.Add(Instruction.Create(OpCodes.Stloc, variableDef));
			    return result;
		    }

		    return result;
	    }

	    List<Instruction> InitializeOutParameter(int argIndex, TypeReference typeRef)
	    {
		    var instructions = new List<Instruction>();
		    if (typeRef.IsByReference)
			    typeRef = typeRef.GetElementType();
		    instructions.Add(Instruction.Create(OpCodes.Ldarg, argIndex));
		    if (IsStruct(typeRef))
		    {
			    instructions.Add(Instruction.Create(OpCodes.Initobj, typeRef));
			    return instructions;
		    }

		    if (IsValue(typeRef))
		    {
			    if (EqualTypeRef(typeRef, ts.Single))
			    {
				    instructions.Add(Instruction.Create(OpCodes.Ldc_R4, (float)0));
				    instructions.Add(Instruction.Create(OpCodes.Stind_R4));
				    return instructions;
			    }
			    else if (EqualTypeRef(typeRef, ts.Double))
			    {
				    instructions.Add(Instruction.Create(OpCodes.Ldc_R8, (double)0));
				    instructions.Add(Instruction.Create(OpCodes.Stind_R8));
				    return instructions;
			    }
			    else if (EqualTypeRef(typeRef, ts.Int64))
			    {
				    instructions.Add(Instruction.Create(OpCodes.Ldc_I8, (long)0));
				    instructions.Add(Instruction.Create(OpCodes.Stind_I8));
				    return instructions;
			    }
			    else
			    {
				    instructions.Add(Instruction.Create(OpCodes.Ldc_I4, 0));
				    instructions.Add(Instruction.Create(OpCodes.Stind_I4));
				    return instructions;
			    }
		    }

		    // class or default
		    instructions.Add(Instruction.Create(OpCodes.Ldnull));
		    instructions.Add(Instruction.Create(OpCodes.Stind_Ref));

		    return instructions;
	    }

	    List<Instruction> EmitCallParameter(
			CompileTimePreludeMethod patch,
			bool allowFirsParamPassthrough,
			out VariableDefinition? tmpInstanceBoxingVar,
			out VariableDefinition? tmpObjectVar,
			out bool refResultUsed,
			List<KeyValuePair<VariableDefinition, TypeReference>> tmpBoxVars
		)
		{
			tmpInstanceBoxingVar = null;
			tmpObjectVar = null;
			refResultUsed = false;
			var result = new List<Instruction>();

			var originalIsStatic = originalDef.IsStatic;
			var returnType = originalDef.ReturnType;
			var patchInjections = injections[patch].ToList();

			var isInstance = originalIsStatic is false;
			var originalParameters = originalDef.Parameters;
			var originalParameterNames = originalParameters.Select(p => p.Name).ToArray();
			var originalType = originalDef.DeclaringType;

			var parameters = patch.Method!.Parameters.ToList();
			if (allowFirsParamPassthrough && !EqualTypeRef(patch.Method!.ReturnType, ts.Void) && parameters.Count > 0 && EqualTypeRef(parameters[0].ParameterType, patch.Method!.ReturnType))
			{
				patchInjections.RemoveAt(0);
				parameters.RemoveAt(0);
			}

			foreach (var injection in patchInjections)
			{
				var injectionType = injection.InjectionType;
				var paramRealName = injection.RealName;
				var paramType = injection.ParameterDef.ParameterType;

				if (injectionType == InjectionType.OriginalMethod)
				{
					if (EmitOriginalBaseMethod(originalDef, result))
						continue;

					result.Add(Instruction.Create(OpCodes.Ldnull));
					continue;
				}

				if (injectionType == InjectionType.Exception)
				{
					if (exceptionVariable != null)
						result.Add(Instruction.Create(OpCodes.Ldloc, exceptionVariable));
					else
						result.Add(Instruction.Create(OpCodes.Ldnull));
					continue;
				}

				if (injectionType == InjectionType.RunOriginal)
				{
					if (runOriginalVariable != null)
						result.Add(Instruction.Create(OpCodes.Ldloc, runOriginalVariable));
					else
						result.Add(Instruction.Create(OpCodes.Ldc_I4_0));
					continue;
				}

				if (injectionType == InjectionType.Instance)
				{
					if (originalIsStatic)
						result.Add(Instruction.Create(OpCodes.Ldnull));
					else
					{
						var parameterIsRef = paramType.IsByReference;
						var parameterIsObject = EqualTypeRef(paramType, ts.Object) || EqualTypeRef(paramType, ts.Object.MakeByReferenceType());

						if (IsStruct(originalType))
						{
							if (parameterIsObject)
							{
								if (parameterIsRef)
								{
									result.Add(Instruction.Create(OpCodes.Ldarg_0));
									result.Add(Instruction.Create(OpCodes.Ldobj, originalType));
									result.Add(Instruction.Create(OpCodes.Box, originalType));
									tmpInstanceBoxingVar = new VariableDefinition(ts.Object);
									result.Add(Instruction.Create(OpCodes.Stloc, tmpInstanceBoxingVar));
									result.Add(Instruction.Create(OpCodes.Ldloca, tmpInstanceBoxingVar));
								}
								else
								{
									result.Add(Instruction.Create(OpCodes.Ldarg_0));
									result.Add(Instruction.Create(OpCodes.Ldobj, originalType));
									result.Add(Instruction.Create(OpCodes.Box, originalType));
								}
							}
							else
							{
								if (parameterIsRef)
									result.Add(Instruction.Create(OpCodes.Ldarg_0));
								else
								{
									result.Add(Instruction.Create(OpCodes.Ldarg_0));
									result.Add(Instruction.Create(OpCodes.Ldobj, originalType));
								}
							}
						}
						else
						{
							if (parameterIsRef)
								result.Add(Instruction.Create(OpCodes.Ldarga, 0));
							else
								result.Add(Instruction.Create(OpCodes.Ldarg_0));
						}
					}
					continue;
				}

				if (injectionType == InjectionType.ArgsArray)
				{
					if (injectedLocals.TryGetValue(InjectionType.ArgsArray, out var argsArrayVar))
						result.Add(Instruction.Create(OpCodes.Ldloc, argsArrayVar));
					else
						result.Add(Instruction.Create(OpCodes.Ldnull));
					continue;
				}

				if (paramRealName.StartsWith(INSTANCE_FIELD_PREFIX, StringComparison.Ordinal))
				{
					var fieldName = paramRealName.Substring(INSTANCE_FIELD_PREFIX.Length);
					FieldDefinition? fieldDef;
					if (fieldName.All(char.IsDigit))
					{
						fieldDef = CompileTimeAccessTools.DeclaredField(originalType, int.Parse(fieldName));
						if (fieldDef is null)
							throw new ArgumentException($"No field found at given index in class {originalType?.AssemblyQualifiedName ?? "null"}", fieldName);
					}
					else
					{
						fieldDef = CompileTimeAccessTools.Field(originalType, fieldName);
						if (fieldDef is null)
							throw new ArgumentException($"No such field defined in class {originalType?.AssemblyQualifiedName ?? "null"}", fieldName);
					}

					if (fieldDef.IsStatic)
						result.Add(Instruction.Create(paramType.IsByReference ? OpCodes.Ldsflda : OpCodes.Ldsfld, fieldDef));
					else
					{
						result.Add(Instruction.Create(OpCodes.Ldarg_0));
						result.Add(Instruction.Create(paramType.IsByReference ? OpCodes.Ldflda : OpCodes.Ldfld, fieldDef));
					}
					continue;
				}

				if (injectionType == InjectionType.State)
				{
					var ldlocCode = paramType.IsByReference ? OpCodes.Ldloca : OpCodes.Ldloc;
					if (otherLocals.TryGetValue(patch.DeclaringType?.AssemblyQualifiedName ?? "null", out var stateVar))
						result.Add(Instruction.Create(ldlocCode, stateVar));
					else
						result.Add(Instruction.Create(OpCodes.Ldnull));
					continue;
				}

				if (injectionType == InjectionType.Result)
				{
					if (returnType.FullName == typeof(void).FullName)
						throw new Exception($"Cannot get result from void method {originalDef.FullDescription()}");
					var resultType = paramType;
					if (resultType.IsByReference && returnType.IsByReference is false)
						resultType = resultType.GetElementType();
					if (IsAssignableFrom(resultType, returnType) is false)
						throw new Exception($"Cannot assign method return type {returnType.FullName} to {InjectedParameter.RESULT_VAR} type {resultType.FullName} for method {originalDef.FullDescription()}");
					var ldlocCode = paramType.IsByReference && returnType.IsByReference is false ? OpCodes.Ldloca : OpCodes.Ldloc;
					if (returnType.IsValueType && EqualTypeRef(paramType, ts.Object.MakeByReferenceType()))
						ldlocCode = OpCodes.Ldloc;
					result.Add(Instruction.Create(ldlocCode, injectedLocals[InjectionType.Result]));
					if (returnType.IsValueType)
					{
						if (EqualTypeRef(paramType, ts.Object))
							result.Add(Instruction.Create(OpCodes.Box, returnType));
						else if (EqualTypeRef(paramType, ts.Object.MakeByReferenceType()))
						{
							result.Add(Instruction.Create(OpCodes.Box, returnType));
							tmpObjectVar = new VariableDefinition(ts.Object);
							result.Add(Instruction.Create(OpCodes.Stloc, tmpObjectVar));
							result.Add(Instruction.Create(OpCodes.Ldloca, tmpObjectVar));
						}
					}
					continue;
				}

				if (injectionType == InjectionType.ResultRef)
				{
					if (!returnType.IsByReference)
						throw new Exception(
							 $"Cannot use {InjectionType.ResultRef} with non-ref return type {returnType.FullName} of method {originalDef.FullDescription()}");

					var resultType = paramType;
					var expectedTypeRef = module.ImportReference(typeof(RefResult<>)).MakeGenericInstanceType(returnType.GetElementType()).MakeByReferenceType();
					if (!EqualTypeRef(resultType, expectedTypeRef))
						throw new Exception(
							 $"Wrong type of {InjectedParameter.RESULT_REF_VAR} for method {originalDef.FullDescription()}. Expected {expectedTypeRef.FullName}, got {resultType.FullName}");

					result.Add(Instruction.Create(OpCodes.Ldloca, injectedLocals[InjectionType.ResultRef]));

					refResultUsed = true;
					continue;
				}

				if (otherLocals.TryGetValue(paramRealName, out var localBuilder))
				{
					var ldlocCode = paramType.IsByReference ? OpCodes.Ldloca : OpCodes.Ldloc;
					result.Add(Instruction.Create(ldlocCode, localBuilder));
					continue;
				}

				int argumentIdx;
				if (paramRealName.StartsWith(PARAM_INDEX_PREFIX, StringComparison.Ordinal))
				{
					var val = paramRealName.Substring(PARAM_INDEX_PREFIX.Length);
					if (!int.TryParse(val, out argumentIdx))
						throw new Exception($"Parameter {paramRealName} does not contain a valid index");
					if (argumentIdx < 0 || argumentIdx >= originalParameters.Count)
						throw new Exception($"No parameter found at index {argumentIdx}");
				}
				else
				{
					argumentIdx = GetArgumentIndex(patch.Method.Resolve(), originalParameterNames, injection.ParameterDef);
					if (argumentIdx == -1)
					{
						var patchMethod = CompileTimePreludeMethod.Merge(CompileTimePreludeMethodUtils.GetFromTypeRef(paramType));
						patchMethod.MethodType ??= MethodType.Normal;
						var delegateOriginalRef = patchMethod.GetOriginalMethod();
						if (delegateOriginalRef != null)
						{
							var delegateOriginalDef = delegateOriginalRef.Resolve();
							var delegateConstructor = CompileTimeAccessTools.DeclaredMethod(paramType, ".ctor", [ts.Object, ts.IntPtr]);
							if (delegateConstructor is not null)
							{
								if (delegateOriginalDef.IsStatic)
									result.Add(Instruction.Create(OpCodes.Ldnull));
								else
								{
									result.Add(Instruction.Create(OpCodes.Ldarg_0));
									if (originalType != null && originalType.IsValueType)
									{
										result.Add(Instruction.Create(OpCodes.Ldobj, originalType));
										result.Add(Instruction.Create(OpCodes.Box, originalType));
									}
								}

								if (delegateOriginalDef.IsStatic is false && patchMethod.NonVirtualDelegate is false)
								{
									result.Add(Instruction.Create(OpCodes.Dup));
									result.Add(Instruction.Create(OpCodes.Ldvirtftn, delegateOriginalDef));
								}
								else
									result.Add(Instruction.Create(OpCodes.Ldftn, delegateOriginalDef));
								result.Add(Instruction.Create(OpCodes.Newobj, delegateConstructor));
								continue;
							}
						}

						throw new Exception($"Parameter \"{paramRealName}\" not found in method {originalDef.FullDescription()}");
					}
				}

				var originalParamType = originalParameters[argumentIdx].ParameterType;
				var originalParamElementType = originalParamType.IsByReference ? originalParamType.GetElementType() : originalParamType;
				var patchParamType = paramType;
				var patchParamElementType = patchParamType.IsByReference ? patchParamType.GetElementType() : patchParamType;
				var originalIsNormal = originalParameters[argumentIdx].IsOut is false && originalParamType.IsByReference is false;
				var patchIsNormal = injection.ParameterDef.IsOut is false && patchParamType.IsByReference is false;
				var needsBoxing = originalParamElementType.IsValueType && patchParamElementType.IsValueType is false;
				var patchArgIndex = argumentIdx + (isInstance ? 1 : 0);

				if (originalIsNormal == patchIsNormal)
				{
					result.Add(Instruction.Create(OpCodes.Ldarg, patchArgIndex));
					if (needsBoxing)
					{
						if (patchIsNormal)
							result.Add(Instruction.Create(OpCodes.Box, originalParamElementType));
						else
						{
							result.Add(Instruction.Create(OpCodes.Ldobj, originalParamElementType));
							result.Add(Instruction.Create(OpCodes.Box, originalParamElementType));
							var tmpBoxVar = new VariableDefinition(patchParamElementType);
							result.Add(Instruction.Create(OpCodes.Stloc, tmpBoxVar));
							result.Add(Instruction.Create(OpCodes.Ldloca_S, tmpBoxVar));
							tmpBoxVars.Add(new KeyValuePair<VariableDefinition, TypeReference>(tmpBoxVar, originalParamElementType));
						}
					}
					continue;
				}

				if (originalIsNormal && patchIsNormal is false)
				{
					if (needsBoxing)
					{
						result.Add(Instruction.Create(OpCodes.Ldarg, patchArgIndex));
						result.Add(Instruction.Create(OpCodes.Box, originalParamElementType));
						var tmpBoxVar = new VariableDefinition(patchParamElementType);
						result.Add(Instruction.Create(OpCodes.Stloc, tmpBoxVar));
						result.Add(Instruction.Create(OpCodes.Ldloca_S, tmpBoxVar));
					}
					else
						result.Add(Instruction.Create(OpCodes.Ldarga, patchArgIndex));
					continue;
				}

				result.Add(Instruction.Create(OpCodes.Ldarg, patchArgIndex));
				if (needsBoxing)
				{
					result.Add(Instruction.Create(OpCodes.Ldobj, originalParamElementType));
					result.Add(Instruction.Create(OpCodes.Box, originalParamElementType));
				}
				else
				{
					if (originalParamElementType.IsValueType)
						result.Add(Instruction.Create(OpCodes.Ldobj, originalParamElementType));
					else
						result.Add(LoadIndOpCodeFor(originalParameters[argumentIdx].ParameterType));
				}
			}
			return result;
		}
	    
	    IEnumerable<(ParameterDefinition info, string realName)> OriginalParameters(MethodReference methodRef)
	    {
		    var methodDef = methodRef.Resolve();
		    var baseArgs = GetArgumentAttributes(methodRef);
		    if (methodRef.DeclaringType is not null)
			    baseArgs = baseArgs.Union(GetArgumentAttributes(methodRef.DeclaringType)).OfType<HarmonyArgument>();
		    return methodRef.Parameters.Select(p =>
		    {
			    var arg = p.GetArgumentAttribute();
			    if (arg != null)
				    return (p, arg.OriginalName ?? p.Name);
			    return (p, baseArgs.GetRealName(p.Name, null) ?? p.Name);
		    });
	    }
	    
	    IEnumerable<HarmonyArgument> GetArgumentAttributes(MethodReference methodRef)
	    {
		    try
		    {
			    var methodDef = methodRef.Resolve();
			    var attributes = methodDef.CustomAttributes;
			    return AllHarmonyArguments(attributes);
		    }
		    catch (NotSupportedException)
		    {
			    return [];
		    }
	    }
	    
	    static IEnumerable<HarmonyArgument> AllHarmonyArguments(IEnumerable<CustomAttribute> attributes)
	    {
		    return attributes.Select(attr =>
			    {
				    if (attr.AttributeType.Name != nameof(HarmonyArgument)) return null;
				    return Activator.CreateInstance(typeof(HarmonyArgument), [..attr.ConstructorArguments.Select(x => x.Value)]);
			    })
			    .OfType<HarmonyArgument>();
	    }
	    
	    List<Instruction> RestoreArgumentArray()
	    {
		    var result = new List<Instruction>();
		    var originalIsStatic = originalDef.IsStatic;
		    var parameters = originalDef.Parameters;
		    var i = 0;
		    var arrayIdx = 0;
		    foreach (var pInfo in parameters)
		    {
			    var argIndex = i++ + (originalIsStatic ? 0 : 1);
			    var pType = pInfo.ParameterType;
			    if (pType.IsByReference)
			    {
				    pType = pType.GetElementType();

				    result.Add(Instruction.Create(OpCodes.Ldarg, argIndex));
				    result.Add(Instruction.Create(OpCodes.Ldloc, injectedLocals[InjectionType.ArgsArray]));
				    result.Add(Instruction.Create(OpCodes.Ldc_I4, arrayIdx));
				    result.Add(Instruction.Create(OpCodes.Ldelem_Ref));

				    if (pType.IsValueType)
				    {
					    result.Add(Instruction.Create(OpCodes.Unbox_Any, pType));
					    if (IsStruct(pType))
						    result.Add(Instruction.Create(OpCodes.Stobj, pType));
					    else
						    result.Add(StoreIndOpCodeFor(pType));
				    }
				    else
				    {
					    result.Add(Instruction.Create(OpCodes.Castclass, pType));
					    result.Add(Instruction.Create(OpCodes.Stind_Ref));
				    }
			    }
			    else
			    {
				    result.Add(Instruction.Create(OpCodes.Ldloc, injectedLocals[InjectionType.ArgsArray]));
				    result.Add(Instruction.Create(OpCodes.Ldc_I4, arrayIdx));
				    result.Add(Instruction.Create(OpCodes.Ldelem_Ref));
				    if (pType.IsValueType)
					    result.Add(Instruction.Create(OpCodes.Unbox_Any, pType));
				    else
					    result.Add(Instruction.Create(OpCodes.Castclass, pType));
				    result.Add(Instruction.Create(OpCodes.Starg, argIndex));
			    }
			    arrayIdx++;
		    }
		    return result;
	    }
	    
	    List<Instruction> FaultRewrite(List<Instruction> originalInstructions)
	    {
		    if (originalInstructions is null) throw new ArgumentNullException(nameof(originalInstructions));

		    var i = 0;
		    var rewritten = new List<CodeInstruction>(originalInstructions.Count * 2);
		    while (i < originalInstructions.Count)
		    {
			    var cur = originalInstructions[i];

			    if (HasBlock(cur, ExceptionBlockType.BeginFaultBlock) == false)
			    {
				    rewritten.Add(new CodeInstruction(cur));
				    ++i;
				    continue;
			    }

			    var beginExceptionIdx = FindMatchingBeginException(rewritten);
			    var endExceptionIdx = FindMatchingEndException(originalInstructions, i + 1);

			    if (beginExceptionIdx < 0 || endExceptionIdx < 0)
				    throw new InvalidOperationException("Unbalanced exception markers – cannot rewrite.");

			    var faultBody = new List<CodeInstruction>();
			    for (var k = i; k < endExceptionIdx; ++k)
				    faultBody.Add(CloneWithoutFaultMarker(originalInstructions[k]));

			    i = endExceptionIdx + 1;

			    var failedLocal = generator.DeclareLocal(typeof(bool));
			    var skipFault = generator.DefineLabel();

			    rewritten.AddRange([
				    Nop.WithBlocks(new ExceptionBlock(ExceptionBlockType.BeginCatchBlock, typeof(object))),
				    Pop,
				    Ldc_I4_1,
				    Stloc[failedLocal.LocalIndex],
				    Rethrow,
				    Nop.WithBlocks(new ExceptionBlock(ExceptionBlockType.BeginFinallyBlock)),
				    Ldloc[failedLocal.LocalIndex],
				    Brfalse_S[skipFault],
				    Nop.WithLabels(skipFault),
				    Nop.WithBlocks(new ExceptionBlock(ExceptionBlockType.EndExceptionBlock))
			    ]);
		    }

		    return rewritten;
	    }

	    Instruction WithLabels(Instruction instr, params Instruction[] instrLabels)
	    {
		    if (!labels.TryGetValue(instr, out var value))
		    {
			    value = [];
		    }
		    value.AddRange(instrLabels);
		    labels.Add(instr, value);
		    return instr;
	    }
	    
	    Instruction NopWithLabels(params Instruction[] instrLabels)
	    {
		    var instr = Instruction.Create(OpCodes.Nop);
		    labels.Add(instr, [..instrLabels]);
		    return instr;
	    }
	    
	    Instruction NopWithLabelList(List<Instruction> instrLabels)
	    {
		    var instr = Instruction.Create(OpCodes.Nop);
		    labels.Add(instr, instrLabels);
		    return instr;
	    }
	    
	    Instruction LoadIndOpCodeFor(TypeReference typeRef)
	    {
		    if (primitivesWithObjectTypeCode.Any(x => EqualTypeRef(x, typeRef)))
			    return Instruction.Create(OpCodes.Ldind_I);

		    return typeRef switch
		    {
			    _ when EqualTypeRef(typeRef, ts.SByte)     || EqualTypeRef(typeRef, ts.Byte)     || EqualTypeRef(typeRef, ts.Boolean) => Instruction.Create(OpCodes.Ldind_I1),
			    _ when EqualTypeRef(typeRef, ts.Char)      || EqualTypeRef(typeRef, ts.Int16)    || EqualTypeRef(typeRef, ts.UInt16)  => Instruction.Create(OpCodes.Ldind_I2),
				_ when EqualTypeRef(typeRef, ts.Int32)     || EqualTypeRef(typeRef, ts.UInt32)   => Instruction.Create(OpCodes.Ldind_I4),
			    _ when EqualTypeRef(typeRef, ts.Int64)     || EqualTypeRef(typeRef, ts.UInt64)   => Instruction.Create(OpCodes.Ldind_I8),
			    _ when EqualTypeRef(typeRef, ts.Single)    => Instruction.Create(OpCodes.Ldind_R4),
			    _ when EqualTypeRef(typeRef, ts.Double)    => Instruction.Create(OpCodes.Ldind_R8),
			    _ when EqualTypeRef(typeRef, dateTimeType) || EqualTypeRef(typeRef, decimalType) => throw new NotSupportedException(),
			    _ when EqualTypeRef(typeRef, emptyType)    || EqualTypeRef(typeRef, ts.Object)   || EqualTypeRef(typeRef, dbNullType) || EqualTypeRef(typeRef, ts.String) => Instruction.Create(OpCodes.Ldind_Ref),
			    _ => Instruction.Create(OpCodes.Ldind_Ref),
		    };
	    }
	    
	    Instruction StoreIndOpCodeFor(TypeReference typeRef)
	    {
		    if (primitivesWithObjectTypeCode.Contains(typeRef))
			    return Instruction.Create(OpCodes.Stind_I);

		    return typeRef switch
		    {
			    _ when EqualTypeRef(typeRef, ts.SByte)     || EqualTypeRef(typeRef, ts.Byte)     || EqualTypeRef(typeRef, ts.Boolean) => Instruction.Create(OpCodes.Stind_I1),
			    _ when EqualTypeRef(typeRef, ts.Char)      || EqualTypeRef(typeRef, ts.Int16)    || EqualTypeRef(typeRef, ts.UInt16)  => Instruction.Create(OpCodes.Stind_I2),
			    _ when EqualTypeRef(typeRef, ts.Int32)     || EqualTypeRef(typeRef, ts.UInt32)   => Instruction.Create(OpCodes.Stind_I4),
			    _ when EqualTypeRef(typeRef, ts.Int64)     || EqualTypeRef(typeRef, ts.UInt64)   => Instruction.Create(OpCodes.Stind_I8),
			    _ when EqualTypeRef(typeRef, ts.Single)    => Instruction.Create(OpCodes.Stind_R4),
			    _ when EqualTypeRef(typeRef, ts.Double)    => Instruction.Create(OpCodes.Stind_R8),
			    _ when EqualTypeRef(typeRef, dateTimeType) || EqualTypeRef(typeRef, decimalType) => throw new NotSupportedException(),
			    _ when EqualTypeRef(typeRef, emptyType)    || EqualTypeRef(typeRef, ts.Object)   || EqualTypeRef(typeRef, dbNullType) || EqualTypeRef(typeRef, ts.String) => Instruction.Create(OpCodes.Stind_Ref),
			    _ => Instruction.Create(OpCodes.Stind_Ref),
		    };
	    }
	    
	    bool EmitOriginalBaseMethod(MethodDefinition original, List<Instruction> codes)
	    {
		    if (original is MethodInfo method)
			    codes.Add(Ldtoken[method]);
		    else if (original is ConstructorInfo constructor)
			    codes.Add(Ldtoken[constructor]);
		    else
			    return false;

		    var type = original.ReflectedType;
		    if (type.IsGenericType)
			    codes.Add(Ldtoken[type]);
		    codes.Add(Call[type.IsGenericType ? m_GetMethodFromHandle2 : m_GetMethodFromHandle1]);
		    return true;
	    }
	    
	    int GetArgumentIndex(MethodDefinition patch, string[] originalParameterNames, ParameterReference patchParam)
	    {
		    var originalName = patchParam.GetRealParameterName(originalParameterNames);
		    if (originalName is not null)
			    return Array.IndexOf(originalParameterNames, originalName);

		    originalName = patch.GetRealParameterName(originalParameterNames, patchParam.Name);
		    if (originalName is not null)
			    return Array.IndexOf(originalParameterNames, originalName);

		    return -1;
	    }
	    
		string? GetRealParameterName(ParameterDefinition parameterDef, string[] originalParameterNames)
	    {
		    var attribute = parameterDef.GetArgumentAttribute();
		    if (attribute is null)
			    return null;

		    if (string.IsNullOrEmpty(attribute.OriginalName) is false)
			    return attribute.OriginalName;

		    if (attribute.Index >= 0 && attribute.Index < originalParameterNames.Length)
			    return originalParameterNames[attribute.Index];

		    return null;
	    }

	    bool EqualTypeRef(TypeReference x, TypeReference y)
	    {
		    return 
	    }

	    bool IsAssignableFrom(TypeReference x, TypeReference y)
	    {
		    return
	    }
    }
    
    private List<Instruction> GenerateInstructions(MethodDefinition originalDef, IEnumerable<CompileTimePreludeMethod> patchMethods)
    {
	    var result = new List<Instruction>();
	    foreach (var patchMethod in patchMethods)
	    {
		    var instr = GenerateInstructions(patchMethod);
		    result.AddRange(instr);
	    }

	    return result;
    }

    private List<Instruction> GenerateInstructions(MethodDefinition originalDef, CompileTimePreludeMethod patchMethod)
    {
	    var result = new List<Instruction>();

    }

    public void Commit(ICompileTimePatchRegistry registry)
    {
	    foreach (var originalDef in registry.GetOriginalMethods())
	    {
		    var prefixes = registry.GetPrefixMethods(originalDef);
		    var postfixes = registry.GetPostfixMethods(originalDef);
		    var finalizers = registry.GetFinalizerMethods(originalDef);

		    DoPatch(originalDef, prefixes, postfixes, finalizers);
	    }
    }
}