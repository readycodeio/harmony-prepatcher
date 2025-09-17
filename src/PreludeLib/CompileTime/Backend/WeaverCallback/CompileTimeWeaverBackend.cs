using System.Reflection;
using HarmonyLib;
using Microsoft.Extensions.Logging;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using MonoMod.Utils;
using MonoMod.Utils.Cil;
using PreludeLib.CompileTime.Public;
using PreludeLib.CompileTime.Registry;
using PreludeLib.CompileTime.Utils;
using static PreludeLib.CompileTime.Utils.CompileTimePreludeCecilUtils;
using EventAttributes = Mono.Cecil.EventAttributes;
using FieldAttributes = Mono.Cecil.FieldAttributes;
using MethodAttributes = Mono.Cecil.MethodAttributes;
using MethodImplAttributes = Mono.Cecil.MethodImplAttributes;
using ParameterAttributes = Mono.Cecil.ParameterAttributes;
using TypeAttributes = Mono.Cecil.TypeAttributes;

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
	
    private readonly TypeReferenceComparer _typeRefComparer = new();
    
    private void DoPatch(
        MethodDefinition originalDef,
        IEnumerable<CompileTimePreludeMethod> prefixes,
        IEnumerable<CompileTimePreludeMethod> postfixes,
        IEnumerable<CompileTimePreludeMethod> finalizers)
    {
        var eventPrefixes = GenerateEventPatches(originalDef, prefixes);
        var eventPostfixes = GenerateEventPatches(originalDef, postfixes);
        var eventFinalizers = GenerateEventPatches(originalDef, finalizers);

        PatchMethod(originalDef, eventPrefixes, eventPostfixes, eventFinalizers);
    }
    
    private IEnumerable<CompileTimePreludeMethod> GenerateEventPatches(MethodDefinition originalDef, IEnumerable<CompileTimePreludeMethod> patchMethods)
	{
		var result = new List<CompileTimePreludeMethod>();
		foreach (var fix in patchMethods)
		{
			var newFix = GenerateEventPatch(originalDef, fix.Method!.Resolve(), fix);
			result.Add(newFix);
		}
		return result;
	}

	private struct PatchEntry
	{
		public FieldReference StaticFieldInstance;
		public EventDefinition EventDef;
		public TypeDefinition DelegateTypeDef;
		public MethodDefinition Method;
	}
    
	private readonly Dictionary<MethodDefinition, PatchEntry> _patchEntries = [];
	
	private CompileTimePreludeMethod GenerateEventPatch(MethodDefinition originalDef, MethodDefinition patchDef, CompileTimePreludeMethod patchMethod)
	{
		CompileTimePreludeMethod result;
		
		if (_patchEntries.TryGetValue(patchDef, out var patchEntry))
		{
			result = new CompileTimePreludeMethod(patchMethod);
			result.StaticFieldInstance = patchEntry.StaticFieldInstance;
			result.Method = patchEntry.Method;
			return result;
		}
		
		patchEntry = new PatchEntry();

		var moduleDef = originalDef.Module;
		
		// Delegate type
		var multicastDelegateRef = moduleDef.ImportReference(typeof(MulticastDelegate));
		var delType = new TypeDefinition(
			originalDef.DeclaringType.Namespace,
			$"{patchDef.DeclaringType.FullName}__{patchDef.Name}__DelegateType",
            attributes: TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.AutoClass | TypeAttributes.AnsiClass,
            baseType: multicastDelegateRef
        );
		moduleDef.Types.Add(delType);

        // .ctor(object, IntPtr) : runtime-provided
        var delCtor = new MethodDefinition(
            ".ctor",
            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
            moduleDef.TypeSystem.Void
        );
        delCtor.ImplAttributes = MethodImplAttributes.Runtime | MethodImplAttributes.Managed;
        delCtor.Parameters.Add(new ParameterDefinition("object", ParameterAttributes.None, moduleDef.TypeSystem.Object));
        delCtor.Parameters.Add(new ParameterDefinition("method",  ParameterAttributes.None, moduleDef.TypeSystem.IntPtr));
        delType.Methods.Add(delCtor);

        // Invoke(float x, object z) : int — runtime-provided
        var delInvoke = new MethodDefinition(
            "Invoke",
            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Virtual,
            moduleDef.ImportReference(patchDef.ReturnType)
        );
        delInvoke.ImplAttributes = MethodImplAttributes.Runtime | MethodImplAttributes.Managed;
        delInvoke.Parameters.AddRange(
	        patchDef.Parameters.Select(x => new ParameterDefinition(x.Name, x.Attributes, moduleDef.ImportReference(x.ParameterType)))
        );
        delType.Methods.Add(delInvoke);

		var typeDef = new TypeDefinition(
			originalDef.DeclaringType.Namespace,
			$"{patchDef.DeclaringType.Name}__{patchDef.Name}__Callback", 
			TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit | TypeAttributes.AutoClass | TypeAttributes.AnsiClass
		);
		moduleDef.Types.Add(typeDef);

		var eventFieldRef = new FieldDefinition(
			"Callback",
			FieldAttributes.Public | FieldAttributes.Static,
			delType
		);
		typeDef.Fields.Add(eventFieldRef);
		
		var combineMethodRef = moduleDef.ImportReference(typeof(Delegate).GetMethod(nameof(Delegate.Combine), new[] { typeof(Delegate), typeof(Delegate) }));
		var removeMethodRef = moduleDef.ImportReference(typeof(Delegate).GetMethod(nameof(Delegate.Remove),  new[] { typeof(Delegate), typeof(Delegate) }));

		// add_SomeDelegate(SomeDelegateType value)
		var add = new MethodDefinition(
			"add_Callback",
			MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig | MethodAttributes.SpecialName,
			moduleDef.TypeSystem.Void
		);
		add.Parameters.Add(new ParameterDefinition("value", ParameterAttributes.None, delType));
		{
			var il = add.Body.GetILProcessor();
			il.Emit(OpCodes.Ldsfld, eventFieldRef);
			il.Emit(OpCodes.Ldarg_0);
			il.Emit(OpCodes.Call, combineMethodRef);
			il.Emit(OpCodes.Castclass, delType);
			il.Emit(OpCodes.Stsfld, eventFieldRef);
			il.Emit(OpCodes.Ret);
		}
		typeDef.Methods.Add(add);
		
		var remove = new MethodDefinition(
			"remove_Callback",
			MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig | MethodAttributes.SpecialName,
			moduleDef.TypeSystem.Void
		);
		remove.Parameters.Add(new ParameterDefinition("value", ParameterAttributes.None, delType));
		{
			var il = remove.Body.GetILProcessor();
			il.Emit(OpCodes.Ldsfld, eventFieldRef);
			il.Emit(OpCodes.Ldarg_0);
			il.Emit(OpCodes.Call, removeMethodRef);
			il.Emit(OpCodes.Castclass, delType);
			il.Emit(OpCodes.Stsfld, eventFieldRef);
			il.Emit(OpCodes.Ret);
		}
		typeDef.Methods.Add(remove);
		
		var callbackEvent = new EventDefinition(
			"Callback",
			EventAttributes.None,
			delType
		);
		callbackEvent.AddMethod = add;
		callbackEvent.RemoveMethod = remove;
		typeDef.Events.Add(callbackEvent);
		
		patchEntry.EventDef = callbackEvent;
		patchEntry.DelegateTypeDef = delType;
		patchEntry.StaticFieldInstance = eventFieldRef;
		patchEntry.Method = delInvoke;
		_patchEntries.Add(patchDef, patchEntry);
		
		result = new CompileTimePreludeMethod(patchMethod);
		result.StaticFieldInstance = eventFieldRef;
		result.Method = delInvoke;
		return result;
	}

    // FIXME: Actually sort
    public static IEnumerable<CompileTimePreludeMethod> GetSortedPatchMethods(MethodDefinition originalDef, IEnumerable<CompileTimePreludeMethod> patchMethods)
	    => patchMethods;
    
    private void PatchMethod(MethodDefinition originalDef, 
	    IEnumerable<CompileTimePreludeMethod> prefixes,
	    IEnumerable<CompileTimePreludeMethod> postfixes,
	    IEnumerable<CompileTimePreludeMethod> finalizers)
    {
        var sortedPrefixes = GetSortedPatchMethods(originalDef, prefixes);
        var sortedPostfixes = GetSortedPatchMethods(originalDef, postfixes);
        var sortedFinalizers = GetSortedPatchMethods(originalDef, finalizers);

        PatchMethod(
            originalDef,
            sortedPrefixes.ToList(),
            sortedPostfixes.ToList(),
            sortedFinalizers.ToList()
        );
    }
    
    private void PatchMethod(MethodDefinition originalDef,
	    List<CompileTimePreludeMethod> prefixes,
	    List<CompileTimePreludeMethod> postfixes,
	    List<CompileTimePreludeMethod> finalizers)
    {
	    var body = originalDef.Body;
	    body.SimplifyMacros();
	    body.InitLocals = true;

	    var il = body.GetILProcessor();
	    var excHelper = new CecilExceptionHelper(il);
	    var module = originalDef.Module;
	    var ts = module.TypeSystem;

	    HashSet<TypeReference> primitivesWithObjectTypeCode = [ts.IntPtr, ts.UIntPtr];
	    var dateTimeType = module.ImportReference(typeof(DateTime));
	    var decimalType = module.ImportReference(typeof(decimal));
	    var emptyType = module.ImportReference(typeof(void));
	    var dbNullType = module.ImportReference(typeof(DBNull));

		var getMethodFromHandle1 = module.ImportReference(typeof(MethodBase).GetMethod("GetMethodFromHandle", [typeof(RuntimeMethodHandle)])!);
		var getMethodFromHandle2 = module.ImportReference(typeof(MethodBase).GetMethod("GetMethodFromHandle", [typeof(RuntimeMethodHandle), typeof(RuntimeTypeHandle)])!);

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
	    Dictionary<Instruction, List<CompileTimeExceptionBlock>> blocks = [];

	    List<(Instruction TryStart, Instruction? TryEnd, List<ExceptionHandler> Handlers)> chains = [];

	    if (fixes.Any() && !EqualTypeRef(originalDef.ReturnType, ts.Void))
	    {
		    resultVariable = new VariableDefinition(originalDef.ReturnType);
		    body.Variables.Add(resultVariable);
		    injectedLocals.Add(InjectionType.Result, resultVariable);
		    instructions.AddRange(GenerateVariableInit(resultVariable, true));
	    }

	    if (AnyFixHas(InjectionType.ResultRef))
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

	    if (AnyFixHas(InjectionType.ArgsArray))
	    {
		    var argsArrayVariable = new VariableDefinition(module.ImportReference(typeof(object[])));
		    body.Variables.Add(argsArrayVariable);
		    injectedLocals.Add(InjectionType.ArgsArray, argsArrayVariable);
		    instructions.AddRange(PrepareArgumentArray());
		    instructions.Add(Instruction.Create(OpCodes.Stloc, argsArrayVariable));
	    }

	    Instruction? skipOriginalLabel = null;
	    VariableDefinition? runOriginalVariable = null;

	    var prefixAffectsOriginal = prefixes.Any(AffectsOriginal);
	    var anyFixHasRunOriginal = AnyFixHas(InjectionType.RunOriginal);
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
		    var varName = $"state__{declaringType.FullName}";
		    _ = otherLocals.TryGetValue(varName, out var maybeLocal);
		    foreach (var injection in InjectionsFor(fix, InjectionType.State))
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
	    var replacement = MethodCopier(true, out var hasReturnCode, out var methodEndsInDeadCode, endLabels);

	    instructions.AddRange(CleanupCodes(replacement, endLabels));

	    if (endLabels.Count > 0)
		    instructions.Add(NopWithLabelList(endLabels));
	    if (resultVariable is not null && hasReturnCode)
		    instructions.Add(Instruction.Create(OpCodes.Stloc, resultVariable));
	    if (skipOriginalLabel != null)
		    instructions.Add(NopWithLabels(skipOriginalLabel));

	    _ = AddPostfixes(false);
	    if (resultVariable is not null && (hasReturnCode || (methodEndsInDeadCode && skipOriginalLabel != null)))
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

	    if (methodEndsInDeadCode == false || skipOriginalLabel is not null || finalizers.Count > 0 || postfixes.Count > 0)
		    instructions.Add(Instruction.Create(OpCodes.Ret));

	    instructions = FaultRewrite(instructions);

	    EmitCodes(instructions);

	    logger.LogDebug("Patching: {Original} ==================", originalDef);
	    foreach (var instr in body.Instructions)
	    {
		    logger.LogDebug(instr.ToString());
	    }
	    
	    return;

	    // ---
	    
	    #region Method copier

	    List<Instruction> MethodCopier(bool stripLastReturn, out bool outHasReturnCode, out bool outMethodEndsInDeadCode, List<Instruction> outEndLabels)
	    {
		    ParseExceptions();
		    return CopierFinalize(stripLastReturn, out outHasReturnCode, out outMethodEndsInDeadCode, outEndLabels);
	    }
	    
	    void ParseExceptions()
	    {
		    foreach (var exception in body.ExceptionHandlers)
		    {
			    var try_start = exception.TryStart;
			    // var try_end = exception.TryOffset + exception.TryLength - 1;

			    var handler_start = exception.HandlerStart;
			    var handler_end = exception.HandlerEnd;

			    var instr1 = try_start;
			    if (!blocks.TryGetValue(instr1, out var instr1Blocks))
			    {
				    instr1Blocks = [];
				    blocks.Add(instr1, instr1Blocks);
			    }
			    
			    instr1Blocks.Add(new CompileTimeExceptionBlock(ExceptionBlockType.BeginExceptionBlock));

			    var instr2 = handler_end;
			    if (!blocks.TryGetValue(instr2, out var instr2Blocks))
			    {
				    instr2Blocks = [];
				    blocks.Add(instr2, instr2Blocks);
			    }
			    instr2Blocks.Add(new CompileTimeExceptionBlock(ExceptionBlockType.EndExceptionBlock));

			    // The FilterOffset property is meaningful only for Filter clauses.
			    // The CatchType property is not meaningful for Filter or Finally clauses.
			    //
			    switch (exception.HandlerType)
			    {
				    case ExceptionHandlerType.Filter:
				    {
					    var instr3 = exception.FilterStart;
					    if (!blocks.TryGetValue(instr3, out var instr3Blocks))
					    {
						    instr3Blocks = [];
						    blocks.Add(instr3, instr3Blocks);
					    }
					    instr3Blocks.Add(new CompileTimeExceptionBlock(ExceptionBlockType.BeginExceptFilterBlock));
					    break;
				    }
				    case ExceptionHandlerType.Finally:
					    var instr4 = handler_start;
					    if (!blocks.TryGetValue(instr4, out var instr4Blocks))
					    {
						    instr4Blocks = [];
						    blocks.Add(instr4, instr4Blocks);
					    }
					    instr4Blocks.Add(new CompileTimeExceptionBlock(ExceptionBlockType.BeginFinallyBlock));
					    break;

				    case ExceptionHandlerType.Catch:
					    var instr5 = handler_start;
					    if (!blocks.TryGetValue(instr5, out var instr5Blocks))
					    {
						    instr5Blocks = [];
						    blocks.Add(instr5, instr5Blocks);
					    }
					    instr5Blocks.Add(new CompileTimeExceptionBlock(ExceptionBlockType.BeginCatchBlock, exception.CatchType));
					    break;

				    case ExceptionHandlerType.Fault:
					    var instr6 = handler_start;
					    if (!blocks.TryGetValue(instr6, out var instr6Blocks))
					    {
						    instr6Blocks = [];
						    blocks.Add(instr6, instr6Blocks);
					    }
					    instr6Blocks.Add(new CompileTimeExceptionBlock(ExceptionBlockType.BeginFaultBlock));
					    break;
			    }
		    }
	    }

	    List<Instruction> CopierFinalize(bool stripLastReturn, out bool outHasReturnCode, out bool outMethodEndsInDeadCode, List<Instruction> outEndLabels)
	    {
		    List<Instruction> result = [..body.Instructions];
		    hasReturnCode = false;
		    methodEndsInDeadCode = false;

		    // pass1 - define labels and add them to instructions that are target of a jump
		    //
		    foreach (var instr in result)
		    {
			    switch (instr.OpCode.OperandType)
			    {
				    case OperandType.InlineSwitch:
				    {
					    var targets = instr.Operand as Instruction[];
					    if (targets is not null)
					    {
						    var newOperand = new List<Instruction>();
						    foreach (var target in targets)
						    {
							    if (!labels.TryGetValue(target, out var targetLabels))
							    {
								    targetLabels = [];
								    labels.Add(target, targetLabels);
							    }
							    var label = Instruction.Create(OpCodes.Nop);
							    targetLabels.Add(label);
							    newOperand.Add(label);
						    }
						    instr.Operand = newOperand.ToArray();
					    }
					    break;
				    }

				    case OperandType.ShortInlineBrTarget:
				    case OperandType.InlineBrTarget:
				    {
					    var target = instr.Operand as Instruction;
					    if (target is not null)
					    {
						    if (!labels.TryGetValue(target, out var targetLabels))
						    {
							    targetLabels = [];
							    labels.Add(target, targetLabels);
						    }
						    var newOperand = Instruction.Create(OpCodes.Nop);
						    targetLabels.Add(newOperand);
						    instr.Operand = newOperand;
					    }
					    break;
				    }
			    }
		    }

		    // pass2 - filter through all processors
		    //
		    // Skipped

		    // pass3 - check for any RET
		    //
		    outHasReturnCode = result.Any(code => code.OpCode == OpCodes.Ret);
		    outMethodEndsInDeadCode = EndsInDeadCode(result);

		    // pass4 - remove RET if it appears at the end
		    //
		    if (stripLastReturn)
		    {
			    while (true)
			    {
				    var lastInstruction = result.LastOrDefault();
				    if (lastInstruction is null || lastInstruction.OpCode != OpCodes.Ret)
					    break;

				    // remember any existing labels
				    if (labels.TryGetValue(lastInstruction, out var lastLabels))
				    {
					    outEndLabels.AddRange(lastLabels);
				    }

				    result.RemoveAt(result.Count - 1);
			    }
		    }

		    return result;
	    }
	    
	    bool EndsInDeadCode(List<Instruction> list)
	    {
		    var n = list.Count;
		    if (n < 2 || list.Last().OpCode != OpCodes.Throw)
			    return false;
		    return list.GetRange(0, n - 1).All(code => code.OpCode != OpCodes.Ret);
	    }
	    
	    #endregion
	    
	    #region Emit body
	    
	    IEnumerable<Instruction> CleanupCodes(List<Instruction> inReplacement, List<Instruction> outEndLabels)
	    {
		    foreach (var instr in inReplacement)
		    {
			    var code = instr.OpCode;
			    if (code == OpCodes.Ret)
			    {
				    var endLabel = Instruction.Create(OpCodes.Nop);
				    var br = Instruction.Create(OpCodes.Br, endLabel);
				    if (labels.TryGetValue(instr, out var instrLabels))
					    labels.Add(br, [..instrLabels]);
				    if (blocks.TryGetValue(instr, out var instrBlocks))
					    blocks.Add(br, [..instrBlocks]);
				    yield return br;
				    outEndLabels.Add(endLabel);
			    }
			    else if (_shortJumps.TryGetValue(code, out var longJump))
			    {
				    var newInstr = CopyInstr(instr);
				    newInstr.OpCode = longJump;
				    yield return newInstr;
			    }
			    else
				    yield return instr;
		    }
	    }
	    
	    void EmitCodes(List<Instruction> newInstructions)
	    {
		    il.Clear();
		    
		    // pass5 - mark labels and exceptions and emit codes
		    //
		    newInstructions.Do(newInstr =>
		    {
			    // start all exception blocks
			    if (blocks.TryGetValue(newInstr, out var instrBlocks))
			    {
				    instrBlocks.Do(EmitMarkBlockBefore);
			    }
			    
			    // mark all labels
			    if (labels.TryGetValue(newInstr, out var instrLabels))
			    {
				    instrLabels.Do(label => il.Append(label));
			    }

			    var code = newInstr.OpCode;
			    var operand = newInstr.Operand;

			    switch (code.OperandType)
			    {
				    case OperandType.InlineNone:
				    {
					    if (IsAnnotation(newInstr) == null)
						    il.Emit(code);
					    break;
				    }
				    case OperandType.InlineSig:
				    {
					    if (operand is null)
						    throw new Exception($"Wrong null argument: {newInstr}");
					    if ((operand is CallSite) is false)
						    throw new Exception($"Wrong Emit argument type {operand.GetType()} in {newInstr}");
					    il.Emit(code, (CallSite)operand);
					    break;
				    }
				    default:
				    {
					    if (operand is null)
						    throw new Exception($"Wrong null argument: {newInstr}");
					    
					    DynEmit(newInstr);
					    break;
				    }
			    }

			    if (instrBlocks != null)
			    {
				    instrBlocks.Do(EmitMarkBlockAfter);
			    }
		    });
	    }
	    
	    void EmitMarkBlockBefore(CompileTimeExceptionBlock block)
	    {
		    switch (block.BlockType)
		    {
			    case ExceptionBlockType.BeginExceptionBlock:
				    excHelper.BeginExceptionBlock();
				    break;

			    case ExceptionBlockType.BeginCatchBlock:
				    excHelper.BeginCatchBlock(block.CatchType);
				    break;

			    case ExceptionBlockType.BeginExceptFilterBlock:
				    excHelper.BeginExceptFilterBlock();
				    break;

			    case ExceptionBlockType.BeginFaultBlock:
				    excHelper.BeginFaultBlock();
				    break;

			    case ExceptionBlockType.BeginFinallyBlock:
				    excHelper.BeginFinallyBlock();
				    break;
		    }
	    }

	    void EmitMarkBlockAfter(CompileTimeExceptionBlock block)
	    {
		    switch (block.BlockType)
		    {
			    case ExceptionBlockType.EndExceptionBlock:
				    excHelper.EndExceptionBlock();
				    break;
		    }
	    }

	    string? IsAnnotation(Instruction instr)
		    => instr.OpCode == OpCodes.Nop ? instr.Operand as string : null;

	    void DynEmit(Instruction instr)
	    {
		    switch (instr.OpCode.OperandType)
		    {
			    case OperandType.InlineBrTarget:
				    il.Emit(instr.OpCode, (Instruction)instr.Operand!); break;
			    case OperandType.InlineField:
				    il.Emit(instr.OpCode, (FieldReference)instr.Operand!); break;
			    case OperandType.InlineI:
				    il.Emit(instr.OpCode, (int)instr.Operand!); break;
			    case OperandType.InlineI8:
				    il.Emit(instr.OpCode, (long)instr.Operand!); break;
			    case OperandType.InlineMethod:
				    il.Emit(instr.OpCode, (MethodReference)instr.Operand!); break;
			    case OperandType.InlineNone:
				    il.Emit(instr.OpCode); break;
			    case OperandType.InlinePhi:
				    il.Emit(instr.OpCode); break;
			    case OperandType.InlineR:
				    il.Emit(instr.OpCode, (double)instr.Operand!); break;
			    case OperandType.InlineSig:
				    il.Emit(instr.OpCode, (CallSite)instr.Operand!); break;
			    case OperandType.InlineString:
				    il.Emit(instr.OpCode, (string)instr.Operand!); break;
			    case OperandType.InlineSwitch:
				    il.Emit(instr.OpCode, (Instruction[])instr.Operand!); break;
			    case OperandType.InlineTok:
				    if (instr.Operand is TypeReference typeRef)
					    il.Emit(instr.OpCode, typeRef);
				    else if (instr.Operand is FieldReference fieldRef)
					    il.Emit(instr.OpCode, fieldRef);
				    else if (instr.Operand is MethodReference methodRef)
					    il.Emit(instr.OpCode, methodRef);
				    else
					    throw new ArgumentException("Invalid operand for InlineTok");
				    break;
			    case OperandType.InlineType:
				    il.Emit(instr.OpCode, (TypeReference)instr.Operand!); break;
			    case OperandType.InlineVar:
				    il.Emit(instr.OpCode, (VariableDefinition)instr.Operand!); break;
			    case OperandType.InlineArg:
				    il.Emit(instr.OpCode, (ParameterDefinition)instr.Operand!); break;
			    case OperandType.ShortInlineBrTarget:
				    il.Emit(instr.OpCode, (Instruction)instr.Operand!); break;
			    case OperandType.ShortInlineI:
				    il.Emit(instr.OpCode, (sbyte)instr.Operand!); break;
			    case OperandType.ShortInlineR:
				    il.Emit(instr.OpCode, (float)instr.Operand!); break;
			    case OperandType.ShortInlineVar:
				    il.Emit(instr.OpCode, (VariableDefinition)instr.Operand!); break;
			    case OperandType.ShortInlineArg:
				    il.Emit(instr.OpCode, (ParameterDefinition)instr.Operand!); break;
			    default:
				    throw new Exception($"Wrong Emit argument type {instr.Operand.GetType()} in {instr}");
		    }
	    }
	    
	    #endregion

	    #region Codegen prefixes, postfixes, finalizers
	    
	    void AddPrefixes()
	    {
		    foreach (var fix in prefixes)
		    {
			    var skipLabel = AffectsOriginal(fix) ? Instruction.Create(OpCodes.Nop) : null;
			    if (skipLabel != null)
			    {
				    instructions.Add(Instruction.Create(OpCodes.Ldloc, runOriginalVariable));
				    instructions.Add(Instruction.Create(OpCodes.Brfalse, skipLabel));
			    }

			    var tmpBoxVars = new List<KeyValuePair<VariableDefinition, TypeReference>>();
			    instructions.AddRange(EmitCallParameter(fix, false, fix.StaticFieldInstance, out var tmpInstanceBoxingVar, out var tmpObjectVar, out var refResultUsed, tmpBoxVars));
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
				    instructions.Add(Instruction.Create(OpCodes.Unbox_Any, GetReturnedType(originalDef)));
				    instructions.Add(Instruction.Create(OpCodes.Stloc, injectedLocals[InjectionType.Result]));
			    }

			    tmpBoxVars.Do(tmpBoxVar =>
			    {
				    instructions.Add(Instruction.Create(originalDef.IsStatic ? OpCodes.Ldarg_0 : OpCodes.Ldarg_1));
				    instructions.Add(Instruction.Create(OpCodes.Ldloc, tmpBoxVar.Key));
				    instructions.Add(Instruction.Create(OpCodes.Unbox_Any, tmpBoxVar.Value));
				    instructions.Add(Instruction.Create(OpCodes.Stobj, tmpBoxVar.Value));
			    });

			    var returnType = fix.Method!.ReturnType;
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
			    instructions.AddRange(EmitCallParameter(fix, true, fix.StaticFieldInstance, out var tmpInstanceBoxingVar, out var tmpObjectVar, out var refResultUsed, tmpBoxVars));
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
			    instructions.AddRange(EmitCallParameter(fix, false, fix.StaticFieldInstance, out var tmpInstanceBoxingVar, out var tmpObjectVar, out var refResultUsed, tmpBoxVars));
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

	    #endregion
	    
	    #region Codegen snippets
	    
	    List<Instruction> PrepareArgumentArray()
	    {
		    var result = new List<Instruction>();
		    var originalIsStatic = originalDef.IsStatic;
		    var parameters = originalDef.Parameters;
		    if (!originalIsStatic)
			    parameters = [body.ThisParameter, ..parameters];
		    var i = 0;
		    foreach (var pInfo in parameters)
		    {
			    if (pInfo.IsOut || pInfo.Attributes.HasFlag(ParameterAttributes.Retval))
				    result.AddRange(InitializeOutParameter(pInfo, pInfo.ParameterType));
		    }

		    result.Add(Instruction.Create(OpCodes.Ldc_I4, parameters.Count));
		    result.Add(Instruction.Create(OpCodes.Newarr, module.ImportReference(typeof(object))));
		    i = 0;
		    var arrayIdx = 0;
		    foreach (var pInfo in parameters)
		    {
			    var pType = pInfo.ParameterType;
			    var paramByRef = pType.IsByReference;
			    if (paramByRef)
				    pType = pType.GetElementType();
			    result.Add(Instruction.Create(OpCodes.Dup));
			    result.Add(Instruction.Create(OpCodes.Ldc_I4, arrayIdx++));
			    result.Add(Instruction.Create(OpCodes.Ldarg_S, pInfo));
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

	    List<Instruction> InitializeOutParameter(ParameterDefinition paramDef, TypeReference typeRef)
	    {
		    var result = new List<Instruction>();
		    if (typeRef.IsByReference)
			    typeRef = typeRef.GetElementType();
		    result.Add(Instruction.Create(OpCodes.Ldarg_S, paramDef));
		    if (IsStruct(typeRef))
		    {
			    result.Add(Instruction.Create(OpCodes.Initobj, typeRef));
			    return result;
		    }

		    if (IsValue(typeRef))
		    {
			    if (EqualTypeRef(typeRef, ts.Single))
			    {
				    result.Add(Instruction.Create(OpCodes.Ldc_R4, (float)0));
				    result.Add(Instruction.Create(OpCodes.Stind_R4));
				    return result;
			    }
			    else if (EqualTypeRef(typeRef, ts.Double))
			    {
				    result.Add(Instruction.Create(OpCodes.Ldc_R8, (double)0));
				    result.Add(Instruction.Create(OpCodes.Stind_R8));
				    return result;
			    }
			    else if (EqualTypeRef(typeRef, ts.Int64))
			    {
				    result.Add(Instruction.Create(OpCodes.Ldc_I8, (long)0));
				    result.Add(Instruction.Create(OpCodes.Stind_I8));
				    return result;
			    }
			    else
			    {
				    result.Add(Instruction.Create(OpCodes.Ldc_I4, 0));
				    result.Add(Instruction.Create(OpCodes.Stind_I4));
				    return result;
			    }
		    }

		    // class or default
		    result.Add(Instruction.Create(OpCodes.Ldnull));
		    result.Add(Instruction.Create(OpCodes.Stind_Ref));

		    return result;
	    }

	    List<Instruction> RestoreArgumentArray()
	    {
		    var result = new List<Instruction>();
		    var originalIsStatic = originalDef.IsStatic;
		    var parameters = originalDef.Parameters;
		    if (!originalIsStatic)
			    parameters = [body.ThisParameter, ..parameters];
		    var i = 0;
		    var arrayIdx = 0;
		    foreach (var pInfo in parameters)
		    {
			    var pType = pInfo.ParameterType;
			    if (pType.IsByReference)
			    {
				    pType = pType.GetElementType();

				    result.Add(Instruction.Create(OpCodes.Ldarg_S, pInfo));
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
				    result.Add(Instruction.Create(OpCodes.Starg, pInfo));
			    }
			    arrayIdx++;
		    }
		    return result;
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
	    
	    bool EmitOriginalBaseMethod(MethodDefinition original, List<Instruction> result)
	    {
			result.Add(Instruction.Create(OpCodes.Ldtoken, original));

		    var type = original.DeclaringType;
		    if (type.IsGenericInstance)
			    result.Add(Instruction.Create(OpCodes.Ldtoken, type));
		    result.Add(Instruction.Create(OpCodes.Call, type.IsGenericInstance ? getMethodFromHandle2 : getMethodFromHandle1));
		    return true;
	    }
	    
	    #endregion

	    #region Type checks
	    
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
	    
	    #endregion
	    
	    #region Codegen method parameters
	    
	    List<Instruction> EmitCallParameter(
			CompileTimePreludeMethod patch,
			bool allowFirsParamPassthrough,
			FieldReference? staticFieldThis,
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
			if (!originalIsStatic)
				originalParameters = [originalDef.Body.ThisParameter, ..originalParameters];
			var originalParameterNames = originalParameters.Select(p => p.Name).ToArray();
			var originalType = originalDef.DeclaringType;

			var patchMethodRef = patch.Method!;
			var patchMethodDef = patchMethodRef.Resolve();
			var parameters = patchMethodDef.Parameters.ToList();

			if (allowFirsParamPassthrough && !EqualTypeRef(patchMethodDef.ReturnType, ts.Void) && parameters.Count > 0 && EqualTypeRef(parameters[0].ParameterType, patch.Method!.ReturnType))
			{
				patchInjections.RemoveAt(0);
				parameters.RemoveAt(0);
			}

			if (staticFieldThis != null)
			{
				result.Add(Instruction.Create(OpCodes.Ldsfld, staticFieldThis!));
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
								result.Add(Instruction.Create(OpCodes.Ldarga, originalDef.Body.ThisParameter));
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
							throw new ArgumentException($"No field found at given index in class {originalType?.FullName ?? "null"}", fieldName);
					}
					else
					{
						fieldDef = CompileTimeAccessTools.Field(originalType, fieldName);
						if (fieldDef is null)
							throw new ArgumentException($"No such field defined in class {originalType?.FullName ?? "null"}", fieldName);
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
					if (otherLocals.TryGetValue($"state__{patch.DeclaringType?.FullName ?? "null"}", out var stateVar))
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
				var patchArgIndex = argumentIdx + (isInstance && staticFieldThis == null ? 1 : 0);
				
				if (originalIsNormal == patchIsNormal)
				{
					result.Add(Instruction.Create(OpCodes.Ldarg_S, originalParameters[patchArgIndex]));
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
						result.Add(Instruction.Create(OpCodes.Ldarg_S, originalParameters[patchArgIndex]));
						result.Add(Instruction.Create(OpCodes.Box, originalParamElementType));
						var tmpBoxVar = new VariableDefinition(patchParamElementType);
						result.Add(Instruction.Create(OpCodes.Stloc, tmpBoxVar));
						result.Add(Instruction.Create(OpCodes.Ldloca_S, tmpBoxVar));
					}
					else
						result.Add(Instruction.Create(OpCodes.Ldarga, originalParameters[patchArgIndex]));
					continue;
				}

				result.Add(Instruction.Create(OpCodes.Ldarg_S, originalParameters[patchArgIndex]));
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
			    var arg = GetArgumentAttribute(p);
			    if (arg != null)
				    return (p, arg.OriginalName ?? p.Name);
			    return (p, baseArgs.GetRealName(p.Name, null) ?? p.Name);
		    });
	    }
	    
	    bool AffectsOriginal(CompileTimePreludeMethod fix)
	    {
		    if (EqualTypeRef(fix.Method!.ReturnType, ts.Boolean))
			    return true;

		    if (injections.TryGetValue(fix, out var injectedParameters) == false)
			    return false;

		    return injectedParameters.Any(parameter =>
		    {
			    if (parameter.InjectionType == InjectionType.Instance)
				    return false;
			    if (parameter.InjectionType == InjectionType.OriginalMethod)
				    return false;
			    if (parameter.InjectionType == InjectionType.State)
				    return false;

			    var p = parameter.ParameterDef;
			    if (p.IsOut || p.Attributes.HasFlag(ParameterAttributes.Retval))
				    return true;
			    var typeRef = p.ParameterType;
			    if (typeRef.IsByReference)
				    return true;
			    if (IsValue(typeRef) is false && IsStruct(typeRef) is false)
				    return true;

			    return false;
		    });
	    }
	    
	    #endregion
	    
	    #region Codegen exceptions
	    
	    List<Instruction> FaultRewrite(List<Instruction> originalInstructions)
	    {
		    if (originalInstructions is null) throw new ArgumentNullException(nameof(originalInstructions));

		    var i = 0;
		    var rewritten = new List<Instruction>(originalInstructions.Count * 2);
		    while (i < originalInstructions.Count)
		    {
			    var cur = originalInstructions[i];

			    if (HasBlock(cur, ExceptionBlockType.BeginFaultBlock) == false)
			    {
				    var newInstr = CopyInstr(cur);
				    rewritten.Add(newInstr);
				    ++i;
				    continue;
			    }

			    var beginExceptionIdx = FindMatchingBeginException(rewritten);
			    var endExceptionIdx = FindMatchingEndException(originalInstructions, i + 1);

			    if (beginExceptionIdx < 0 || endExceptionIdx < 0)
				    throw new InvalidOperationException("Unbalanced exception markers – cannot rewrite.");

			    // var faultBody = new List<Instruction>();
			    // for (var k = i; k < endExceptionIdx; ++k)
				//     faultBody.Add(CloneWithoutFaultMarker(originalInstructions[k]));

			    i = endExceptionIdx + 1;

			    var failedLocal = new VariableDefinition(ts.Boolean);
			    var skipFault = Instruction.Create(OpCodes.Nop);

				var excTypeRef = module.ImportReference(typeof(Exception));
			    rewritten.Add(NopWithBlocks(new CompileTimeExceptionBlock(ExceptionBlockType.BeginCatchBlock, excTypeRef)));
			    rewritten.Add(Instruction.Create(OpCodes.Pop));
			    rewritten.Add(Instruction.Create(OpCodes.Ldc_I4_1));
			    rewritten.Add(Instruction.Create(OpCodes.Stloc, failedLocal.Index));
			    rewritten.Add(Instruction.Create(OpCodes.Rethrow));
			    rewritten.Add(NopWithBlocks(new CompileTimeExceptionBlock(ExceptionBlockType.BeginFinallyBlock)));
			    rewritten.Add(Instruction.Create(OpCodes.Ldloc, failedLocal.Index));
			    rewritten.Add(Instruction.Create(OpCodes.Brfalse_S, skipFault));
			    rewritten.Add(NopWithLabels(skipFault));
			    rewritten.Add(NopWithBlocks(new CompileTimeExceptionBlock(ExceptionBlockType.EndExceptionBlock)));
		    }

		    return rewritten;
	    }
	    
	    Instruction CloneWithoutFaultMarker(Instruction instr)
	    {
		    var copy = Instruction.Create(instr.OpCode);
		    copy.Operand = instr.Operand;
		    if (labels.TryGetValue(instr, out var instrLabels))
			    labels.Add(copy, [..instrLabels]);
		    if (blocks.TryGetValue(instr, out var instrBlocks))
			    blocks.Add(copy, [..instrBlocks.Where(b => b.BlockType != ExceptionBlockType.BeginFaultBlock)]);
		    return copy;
	    }
	    
	    int FindMatchingBeginException(List<Instruction> rewritten)
	    {
		    for (int j = rewritten.Count - 1, depth = 0; j >= 0; --j)
		    {
			    if (HasBlock(rewritten[j], ExceptionBlockType.EndExceptionBlock)) ++depth;
			    if (HasBlock(rewritten[j], ExceptionBlockType.BeginExceptionBlock))
			    {
				    if (depth == 0) return j;
				    --depth;
			    }
		    }
		    return -1;
	    }

	    int FindMatchingEndException(List<Instruction> source, int start)
	    {
		    for (int j = start, depth = 0; j < source.Count; ++j)
		    {
			    if (HasBlock(source[j], ExceptionBlockType.BeginExceptionBlock)) ++depth;
			    if (HasBlock(source[j], ExceptionBlockType.EndExceptionBlock))
			    {
				    if (depth == 0) return j;
				    --depth;
			    }
		    }
		    return -1;
	    }

	    Instruction CopyInstr(Instruction instr)
	    {
		    var newInstr = instr.GetPrototype();
		    
		    if (blocks.TryGetValue(instr, out var instrBlocks))
			    blocks.Add(newInstr, [..instrBlocks]);
		    if (labels.TryGetValue(instr, out var instrLabels))
			    labels.Add(newInstr, [..instrLabels]);
		    return newInstr;
	    }
	    
	    #endregion
	    
	    #region Block

	    Instruction MarkBlock(ExceptionBlockType blockType)
	    {
		    var instr = Instruction.Create(OpCodes.Nop);
		    var excTypeRef = module.ImportReference(typeof(Exception));
		    blocks.Add(instr, [new CompileTimeExceptionBlock(blockType, excTypeRef)]);
		    return instr;
	    }
	    
	    bool HasBlock(Instruction instr, ExceptionBlockType type)
		    => blocks.TryGetValue(instr, out var instrBlocks) && instrBlocks.Any(block => block.BlockType == type);

	    Instruction WithBlocks(Instruction instr, params CompileTimeExceptionBlock[] instrBlocks)
	    {
		    if (!blocks.TryGetValue(instr, out var value))
		    {
			    value = [];
		    }
		    value.AddRange(instrBlocks);
		    blocks.Add(instr, value);
		    return instr;
	    }

	    Instruction NopWithBlocks(params CompileTimeExceptionBlock[] instrBlocks)
	    {
		    var nop = Instruction.Create(OpCodes.Nop);
		    return WithBlocks(nop, instrBlocks);
	    }
	    
	    #endregion

	    #region Labels
	    
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
		    var nop = Instruction.Create(OpCodes.Nop);
		    return WithLabels(nop, instrLabels);
	    }
	    
	    Instruction NopWithLabelList(List<Instruction> instrLabels)
	    {
		    var instr = Instruction.Create(OpCodes.Nop);
		    labels.Add(instr, instrLabels);
		    return instr;
	    }
	    
	    #endregion
	    
	    bool AnyFixHas(InjectionType type)
		    => injections.Values.SelectMany(list => list).Any(pair => pair.InjectionType == type);
    
	    IEnumerable<InjectedParameter> InjectionsFor(CompileTimePreludeMethod fix, InjectionType type = InjectionType.Unknown)
	    {
		    if (injections.TryGetValue(fix, out var list))
		    {
			    if (type != InjectionType.Unknown)
				    return list.Where(pair => pair.InjectionType == type);
			    return list;
		    }
		    return [];
	    }

	    bool EqualTypeRef(TypeReference x, TypeReference y)
		    => _typeRefComparer.Equals(x, y);
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