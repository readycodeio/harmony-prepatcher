using System.Reflection;
using HarmonyLib;
using Microsoft.Extensions.Logging;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using PreludeLib.Compat;
using PreludeLib.CompileTime.Public;
using PreludeLib.CompileTime.Utils;
using static PreludeLib.CompileTime.Utils.CompileTimePreludeCecilUtils;
using EventAttributes = Mono.Cecil.EventAttributes;
using FieldAttributes = Mono.Cecil.FieldAttributes;
using MethodAttributes = Mono.Cecil.MethodAttributes;
using MethodBody = Mono.Cecil.Cil.MethodBody;
using MethodImplAttributes = Mono.Cecil.MethodImplAttributes;
using ParameterAttributes = Mono.Cecil.ParameterAttributes;
using TypeAttributes = Mono.Cecil.TypeAttributes;

namespace PreludeLib.CompileTime.Backend.WeaverCallback;

public class CompileTimeWeaverBackend(ILogger logger) : CompileTimeBackendBase(logger)
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

    private struct PatchEntry
    {
        public FieldReference StaticFieldInstance;
        public EventDefinition EventDef;
        public TypeDefinition DelegateTypeDef;
        public MethodDefinition Method;
    }

    private readonly TypeReferenceComparer _typeRefComparer = new();
    private readonly Dictionary<MethodDefinition, PatchEntry> _patchEntries = [];
    private MethodBodyRestoreHelper _restoreHelper = new();

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
            patchDef.DeclaringType.Namespace,
            $"{patchDef.DeclaringType.Name}__{patchDef.Name}__DelegateType",
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
        delCtor.Parameters.Add(new ParameterDefinition("method", ParameterAttributes.None, moduleDef.TypeSystem.IntPtr));
        delType.Methods.Add(delCtor);

        // Invoke(float x, object z) : int — runtime-provided
        var delInvoke = new MethodDefinition(
            "Invoke",
            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Virtual,
            moduleDef.ImportReference(patchDef.ReturnType)
        );
        delInvoke.ImplAttributes = MethodImplAttributes.Runtime | MethodImplAttributes.Managed;
        foreach (var param in patchDef.Parameters.Select(x => new ParameterDefinition(x.Name, x.Attributes, moduleDef.ImportReference(x.ParameterType))
                 {
                     Constant = x.Constant
                 }))
        {
            delInvoke.Parameters.Add(param);
        }

        delType.Methods.Add(delInvoke);

        var typeDef = new TypeDefinition(
            patchDef.DeclaringType.Namespace,
            $"{patchDef.DeclaringType.Name}__{patchDef.Name}__Callback",
            TypeAttributes.Class | TypeAttributes.Public |
            TypeAttributes.Abstract | TypeAttributes.Sealed |
            TypeAttributes.AnsiClass | TypeAttributes.BeforeFieldInit
        )
        {
            BaseType = moduleDef.TypeSystem.Object
        };
        moduleDef.Types.Add(typeDef);

        var eventFieldRef = new FieldDefinition(
            "CallbackField",
            FieldAttributes.Public | FieldAttributes.Static,
            delType
        );
        typeDef.Fields.Add(eventFieldRef);

        var combineMethodRef = moduleDef.ImportReference(typeof(Delegate).GetMethod(nameof(Delegate.Combine), new[] { typeof(Delegate), typeof(Delegate) }));
        var removeMethodRef = moduleDef.ImportReference(typeof(Delegate).GetMethod(nameof(Delegate.Remove), new[] { typeof(Delegate), typeof(Delegate) }));

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

    private struct OriginalMethod(MethodDefinition methodDef)
    {
        public readonly MethodDefinition MethodDef = methodDef;
        public readonly Dictionary<CompileTimePreludeMethod, List<InjectedParameter>> Injections = [];
        public readonly Dictionary<InjectionType, VariableDefinition> InjectedLocals = [];
        public readonly Dictionary<string, VariableDefinition> OtherLocals = [];
        public VariableDefinition? ExceptionVariable;
        public VariableDefinition? RunOriginalVariable;
        public VariableDefinition? FinalizedVariable;
        public VariableDefinition? ResultVariable;
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

    protected override void DoPatch(
        MethodDefinition original,
        List<CompileTimePreludeMethod> prefixes,
        List<CompileTimePreludeMethod> postfixes,
        List<CompileTimePreludeMethod> finalizers,
        List<CompileTimePreludeMethod> addedPrefixes,
        List<CompileTimePreludeMethod> addedPostfixes,
        List<CompileTimePreludeMethod> addedFinalizers)
    {
        // NOTE: Restore should be first to avoid unnecessary work
        _restoreHelper.Restore(original);
        _restoreHelper.SaveIfNotSaved(original);

        var eventPrefixes = GenerateEventPatches(original, prefixes).ToList();
        var eventPostfixes = GenerateEventPatches(original, postfixes).ToList();
        var eventFinalizers = GenerateEventPatches(original, finalizers).ToList();

        DoPatch(original, eventPrefixes, eventPostfixes, eventFinalizers);
    }

    protected void DoPatch(MethodDefinition originalDef,
        List<CompileTimePreludeMethod> prefixes,
        List<CompileTimePreludeMethod> postfixes,
        List<CompileTimePreludeMethod> finalizers)
    {
        var original = new OriginalMethod(originalDef);

        var body = originalDef.Body;
        body.SimplifyMacros();
        body.InitLocals = true;

        var flow = new CecilFlowHelper();

        var module = originalDef.Module;
        var ts = module.TypeSystem;

        var fixes = prefixes.Concat(postfixes).Concat(finalizers).ToList();
        foreach (var d in fixes.ToDictionary(
                     fix => fix,
                     fix => fix.Method!.Parameters.Select(p => new InjectedParameter(fix.Method.Resolve(), p)).ToList()
                 ))
        {
            original.Injections.Add(d.Key, d.Value);
        }

        if (fixes.Any() && !EqualTypeRef(originalDef.ReturnType, ts.Void))
        {
            original.ResultVariable = new VariableDefinition(originalDef.ReturnType);
            body.Variables.Add(original.ResultVariable);
            original.InjectedLocals.Add(InjectionType.Result, original.ResultVariable);
            flow.AppendAll(GenerateVariableInit(original.ResultVariable, true, module));
        }

        if (AnyFixHas(original, InjectionType.ResultRef))
        {
            if (originalDef.ReturnType.IsByReference)
            {
                var varType = module.ImportReference(typeof(RefResult<>)).MakeGenericInstanceType(originalDef.ReturnType.GetElementType());
                var resultRefVariable = new VariableDefinition(varType);
                body.Variables.Add(resultRefVariable);
                original.InjectedLocals.Add(InjectionType.ResultRef, resultRefVariable);
                flow.Append(Instruction.Create(OpCodes.Ldnull));
                flow.Append(Instruction.Create(OpCodes.Stloc, resultRefVariable));
            }
        }

        if (AnyFixHas(original, InjectionType.ArgsArray))
        {
            var argsArrayVariable = new VariableDefinition(module.ImportReference(typeof(object[])));
            body.Variables.Add(argsArrayVariable);
            original.InjectedLocals.Add(InjectionType.ArgsArray, argsArrayVariable);
            flow.AppendAll(PrepareArgumentArray(originalDef));
            flow.Append(Instruction.Create(OpCodes.Stloc, argsArrayVariable));
        }

        CecilLabel? skipOriginalLabel = null;

        var prefixAffectsOriginal = prefixes.Any(fix => AffectsOriginal(original, fix));
        var anyFixHasRunOriginal = AnyFixHas(original, InjectionType.RunOriginal);
        if (prefixAffectsOriginal || anyFixHasRunOriginal)
        {
            original.RunOriginalVariable = new VariableDefinition(module.ImportReference(typeof(bool)));
            body.Variables.Add(original.RunOriginalVariable);
            flow.Append(Instruction.Create(OpCodes.Ldc_I4_1));
            flow.Append(Instruction.Create(OpCodes.Stloc, original.RunOriginalVariable));
            if (prefixAffectsOriginal)
                skipOriginalLabel = flow.DefineLabel();
        }

        fixes.ForEach(fix =>
        {
            var varName = $"__state_{original.MethodDef.Name}"; // TODO: This will fail if there are multiple patches on the same method that use state
            _ = original.OtherLocals.TryGetValue(varName, out var maybeLocal);
            foreach (var injection in InjectionsFor(original, fix, InjectionType.State))
            {
                var parameterType = injection.ParameterDef.ParameterType;
                var type = parameterType.IsByReference ? parameterType.GetElementType() : parameterType;
                if (maybeLocal != null)
                    continue;
                var privateStateVariable = new VariableDefinition(type);
                body.Variables.Add(privateStateVariable);
                original.OtherLocals.Add(varName, privateStateVariable);
                flow.AppendAll(GenerateVariableInit(privateStateVariable, false, module));
            }
        });

        if (finalizers.Count > 0)
        {
            original.FinalizedVariable = new VariableDefinition(module.ImportReference(typeof(bool)));
            body.Variables.Add(original.FinalizedVariable);
            flow.AppendAll(GenerateVariableInit(original.FinalizedVariable, false, module));
            original.ExceptionVariable = new VariableDefinition(module.ImportReference(typeof(Exception)));
            body.Variables.Add(original.ExceptionVariable);
            original.InjectedLocals.Add(InjectionType.Exception, original.ExceptionVariable);
            flow.AppendAll(GenerateVariableInit(original.ExceptionVariable, false, module));
            // begin try
            flow.Append(MarkBlock(flow, ExceptionBlockType.BeginExceptionBlock, module));
        }

        AddPrefixes(flow, original, prefixes);
        if (skipOriginalLabel != null)
        {
            flow.Append(Instruction.Create(OpCodes.Ldloc, original.RunOriginalVariable));
            flow.Append(Instruction.Create(OpCodes.Brfalse, skipOriginalLabel.Value.Instruction));
        }

        var endLabels = new List<CecilLabel>();
        var replacement = MethodCopier(body, true, out var hasReturnCode, out var methodEndsInDeadCode, endLabels);

        CleanupCodes(replacement, endLabels);
        flow.AppendFlow(replacement);

        if (endLabels.Count > 0)
            flow.Append(NopWithLabels(flow, endLabels));
        if (original.ResultVariable is not null && hasReturnCode)
            flow.Append(Instruction.Create(OpCodes.Stloc, original.ResultVariable));
        if (skipOriginalLabel != null)
            flow.Append(NopWithLabels(flow, skipOriginalLabel.Value));

        _ = AddPostfixes(flow, false, original, postfixes);
        if (original.ResultVariable is not null && (hasReturnCode || (methodEndsInDeadCode && skipOriginalLabel != null)))
            flow.Append(Instruction.Create(OpCodes.Ldloc, original.ResultVariable));

        var needsToStorePassthroughResult = AddPostfixes(flow, true, original, postfixes);

        if (finalizers.Count > 0)
        {
            original.ExceptionVariable = original.InjectedLocals[InjectionType.Exception];

            if (needsToStorePassthroughResult)
            {
                flow.Append(Instruction.Create(OpCodes.Stloc, original.ResultVariable));
                flow.Append(Instruction.Create(OpCodes.Ldloc, original.ResultVariable));
            }

            _ = AddFinalizers(flow, false, original, finalizers);
            flow.Append(Instruction.Create(OpCodes.Ldc_I4_1));
            flow.Append(Instruction.Create(OpCodes.Stloc, original.FinalizedVariable));
            var noExceptionLabel1 = flow.DefineLabel();
            flow.Append(Instruction.Create(OpCodes.Ldloc, original.ExceptionVariable));
            flow.Append(Instruction.Create(OpCodes.Brfalse, noExceptionLabel1.Instruction));
            flow.Append(Instruction.Create(OpCodes.Ldloc, original.ExceptionVariable));
            flow.Append(Instruction.Create(OpCodes.Throw));
            flow.Append(NopWithLabels(flow, noExceptionLabel1));

            // end try, begin catch
            flow.Append(MarkBlock(flow, ExceptionBlockType.BeginCatchBlock, module));
            flow.Append(Instruction.Create(OpCodes.Stloc, original.ExceptionVariable));

            flow.Append(Instruction.Create(OpCodes.Ldloc, original.FinalizedVariable));
            var endFinalizerLabel = flow.DefineLabel();
            flow.Append(Instruction.Create(OpCodes.Brtrue, endFinalizerLabel.Instruction));

            var rethrowPossible = AddFinalizers(flow, true, original, finalizers);

            flow.Append(NopWithLabels(flow, endFinalizerLabel));

            var noExceptionLabel2 = flow.DefineLabel();
            flow.Append(Instruction.Create(OpCodes.Ldloc, original.ExceptionVariable));
            flow.Append(Instruction.Create(OpCodes.Brfalse, noExceptionLabel2.Instruction));
            if (rethrowPossible)
                flow.Append(Instruction.Create(OpCodes.Rethrow));
            else
            {
                flow.Append(Instruction.Create(OpCodes.Ldloc, original.ExceptionVariable));
                flow.Append(Instruction.Create(OpCodes.Throw));
            }

            flow.Append(NopWithLabels(flow, noExceptionLabel2));

            // end catch
            flow.Append(MarkBlock(flow, ExceptionBlockType.EndExceptionBlock, module));

            if (original.ResultVariable is not null)
                flow.Append(Instruction.Create(OpCodes.Ldloc, original.ResultVariable));
        }

        if (methodEndsInDeadCode == false || skipOriginalLabel is not null || finalizers.Count > 0 || postfixes.Count > 0)
            flow.Append(Instruction.Create(OpCodes.Ret));

        flow = FaultRewrite(flow, module);

        var il = body.GetILProcessor();

        EmitCodes(il, flow);

        // Logger.LogDebug("Patching: {Original} ==================", originalDef.FullDescription());
        // foreach (var instr in body.Instructions)
        // {
        //  Logger.LogDebug(instr.ToString());
        // }
    }

    #region Method copier

    private CecilFlowHelper MethodCopier(MethodBody body, bool stripLastReturn, out bool hasReturnCode, out bool methodEndsInDeadCode, List<CecilLabel> outEndLabels)
    {
        var outFlow = new CecilFlowHelper();
        ParseExceptions(outFlow, body);
        CopierFinalize(outFlow, body, stripLastReturn, out hasReturnCode, out methodEndsInDeadCode, outEndLabels);
        return outFlow;
    }

    private void ParseExceptions(CecilFlowHelper outFlow, MethodBody body)
    {
        foreach (var exception in body.ExceptionHandlers)
        {
            outFlow.AddBlock(exception.TryStart, new CecilExceptionBlock(ExceptionBlockType.BeginExceptionBlock));
            outFlow.AddBlock(exception.HandlerEnd.Previous, new CecilExceptionBlock(ExceptionBlockType.EndExceptionBlock));

            // The FilterOffset property is meaningful only for Filter clauses.
            // The CatchType property is not meaningful for Filter or Finally clauses.

            switch (exception.HandlerType)
            {
                case ExceptionHandlerType.Filter:
                {
                    outFlow.AddBlock(exception.FilterStart, new CecilExceptionBlock(ExceptionBlockType.BeginExceptFilterBlock));
                    break;
                }
                case ExceptionHandlerType.Finally:
                    outFlow.AddBlock(exception.HandlerStart, new CecilExceptionBlock(ExceptionBlockType.BeginFinallyBlock));
                    break;

                case ExceptionHandlerType.Catch:
                    outFlow.AddBlock(exception.HandlerStart, new CecilExceptionBlock(ExceptionBlockType.BeginCatchBlock, exception.CatchType));
                    break;

                case ExceptionHandlerType.Fault:
                    outFlow.AddBlock(exception.HandlerStart, new CecilExceptionBlock(ExceptionBlockType.BeginFaultBlock));
                    break;
            }
        }
    }

    private void CopierFinalize(CecilFlowHelper outFlow, MethodBody body, bool stripLastReturn, out bool hasReturnCode, out bool methodEndsInDeadCode, List<CecilLabel> outEndLabels)
    {
        outFlow.AppendAll(body.Instructions);
        hasReturnCode = false;
        methodEndsInDeadCode = false;

        // pass1 - define labels and add them to instructions that are target of a jump
        //
        foreach (var instr in body.Instructions)
        {
            switch (instr.OpCode.OperandType)
            {
                case OperandType.InlineSwitch:
                {
                    if (instr.Operand is Instruction[] targets)
                    {
                        var newOperand = new List<Instruction>();
                        foreach (var target in targets)
                        {
                            var label = outFlow.DefineLabel();
                            outFlow.AddLabel(target, label);
                            newOperand.Add(label.Instruction);
                        }

                        instr.Operand = newOperand.ToArray();
                    }

                    break;
                }

                case OperandType.ShortInlineBrTarget:
                case OperandType.InlineBrTarget:
                {
                    if (instr.Operand is Instruction target)
                    {
                        var label = outFlow.DefineLabel();
                        var newOperand = label.Instruction;
                        outFlow.AddLabel(target, label);
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
        hasReturnCode = outFlow.Instructions.Any(code => code.OpCode == OpCodes.Ret);
        methodEndsInDeadCode = EndsInDeadCode(outFlow);

        // pass4 - remove RET if it appears at the end
        //
        if (stripLastReturn)
        {
            while (true)
            {
                var lastInstruction = outFlow.Instructions.LastOrDefault();
                if (lastInstruction is null || lastInstruction.OpCode != OpCodes.Ret)
                    break;

                // remember any existing labels
                if (outFlow.TryGetLabels(lastInstruction, out var lastLabels))
                {
                    outEndLabels.AddRange(lastLabels);
                }

                outFlow.Remove(lastInstruction);
            }
        }
    }

    private bool EndsInDeadCode(CecilFlowHelper flow)
    {
        var list = flow.Instructions;
        var n = list.Count;
        if (n < 2 || list.Last().OpCode != OpCodes.Throw)
            return false;
        return list.SkipLast(1).All(code => code.OpCode != OpCodes.Ret);
    }

    private void CleanupCodes(CecilFlowHelper inOutFlow, List<CecilLabel> outEndLabels)
    {
        foreach (var instr in inOutFlow.Instructions.ToList())
        {
            var code = instr.OpCode;
            if (code == OpCodes.Ret)
            {
                var endLabel = inOutFlow.DefineLabel();
                var br = Instruction.Create(OpCodes.Br, endLabel.Instruction);
                inOutFlow.Replace(instr, br);
                outEndLabels.Add(endLabel);
            }
            else if (_shortJumps.TryGetValue(code, out var longJump))
            {
                var newInstr = instr.GetPrototype();
                newInstr.OpCode = longJump;
                inOutFlow.Replace(instr, newInstr);
            }
        }
    }

    #endregion

    #region Emit body

    private void EmitCodes(ILProcessor il, CecilFlowHelper flow)
    {
        il.Clear();
        il.Body.ExceptionHandlers.Clear();

        var excHelper = new CecilEmitExceptionHelper(il);

        // pass5 - mark labels and exceptions and emit codes
        //
        flow.Instructions.Do(newInstr =>
        {
            // start all exception blocks
            if (flow.TryGetBlocks(newInstr, out var instrBlocks))
            {
                instrBlocks.Do(x => EmitMarkBlockBefore(excHelper, x));
            }

            // mark all labels
            if (flow.TryGetLabels(newInstr, out var instrLabels))
            {
                instrLabels.Do(label => il.Append(label.Instruction));
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

                    DynEmit(il, newInstr);
                    break;
                }
            }

            if (flow.TryGetBlocks(newInstr, out instrBlocks))
            {
                instrBlocks.Do(x => EmitMarkBlockAfter(excHelper, x));
            }
        });
    }

    private void EmitMarkBlockBefore(CecilEmitExceptionHelper excHelper, CecilExceptionBlock block)
    {
        switch (block.BlockType)
        {
            case ExceptionBlockType.BeginExceptionBlock:
                excHelper.BeginExceptionBlock();
                break;

            case ExceptionBlockType.BeginCatchBlock:
                excHelper.BeginCatchBlock(block.CatchType!);
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

    private void EmitMarkBlockAfter(CecilEmitExceptionHelper excHelper, CecilExceptionBlock block)
    {
        switch (block.BlockType)
        {
            case ExceptionBlockType.EndExceptionBlock:
                excHelper.EndExceptionBlock();
                break;
        }
    }

    private string? IsAnnotation(Instruction instr)
        => instr.OpCode == OpCodes.Nop ? instr.Operand as string : null;

    private void DynEmit(ILProcessor il, Instruction instr)
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

    private void AddPrefixes(CecilFlowHelper outFlow, OriginalMethod original, IEnumerable<CompileTimePreludeMethod> prefixes)
    {
        var originalDef = original.MethodDef;
        var ts = originalDef.Module.TypeSystem;

        foreach (var fix in prefixes)
        {
            var skipLabel = AffectsOriginal(original, fix) ? outFlow.DefineLabel() : (CecilLabel?)null;
            if (skipLabel != null)
            {
                outFlow.Append(Instruction.Create(OpCodes.Ldloc, original.RunOriginalVariable));
                outFlow.Append(Instruction.Create(OpCodes.Brfalse, skipLabel.Value.Instruction));
            }

            var tmpBoxVars = new List<KeyValuePair<VariableDefinition, TypeReference>>();
            outFlow.AppendAll(
                EmitCallParameterAndCall(
                    outFlow,
                    original,
                    fix,
                    false,
                    fix.StaticFieldInstance,
                    out var tmpInstanceBoxingVar,
                    out var tmpObjectVar,
                    out var refResultUsed,
                    tmpBoxVars
                )
            );
            if (OriginalParameters(fix.Method!).Any(pair => pair.realName == MethodPatcherTools.ARGS_ARRAY_VAR))
                outFlow.AppendAll(RestoreArgumentArray(original));
            if (tmpInstanceBoxingVar != null)
            {
                outFlow.Append(Instruction.Create(OpCodes.Ldarg_0));
                outFlow.Append(Instruction.Create(OpCodes.Ldloc, tmpInstanceBoxingVar));
                outFlow.Append(Instruction.Create(OpCodes.Unbox_Any, originalDef.DeclaringType));
                outFlow.Append(Instruction.Create(OpCodes.Stobj, originalDef.DeclaringType));
            }

            if (refResultUsed)
            {
                var label = outFlow.DefineLabel();
                outFlow.Append(Instruction.Create(OpCodes.Ldloc, original.InjectedLocals[InjectionType.ResultRef]));
                outFlow.Append(Instruction.Create(OpCodes.Brfalse_S, label.Instruction));

                outFlow.Append(Instruction.Create(OpCodes.Ldloc, original.InjectedLocals[InjectionType.ResultRef]));
                outFlow.Append(Instruction.Create(OpCodes.Callvirt, CompileTimeAccessTools.Method(original.InjectedLocals[InjectionType.ResultRef].VariableType, "Invoke")));
                outFlow.Append(Instruction.Create(OpCodes.Stloc, original.InjectedLocals[InjectionType.Result]));
                outFlow.Append(Instruction.Create(OpCodes.Ldnull));
                outFlow.Append(Instruction.Create(OpCodes.Stloc, original.InjectedLocals[InjectionType.ResultRef]));

                var instr = Instruction.Create(OpCodes.Nop);
                outFlow.AddLabel(instr, label);
                outFlow.Append(instr);
            }
            else if (tmpObjectVar != null)
            {
                outFlow.Append(Instruction.Create(OpCodes.Ldloc, tmpObjectVar));
                outFlow.Append(Instruction.Create(OpCodes.Unbox_Any, GetReturnedType(originalDef)));
                outFlow.Append(Instruction.Create(OpCodes.Stloc, original.InjectedLocals[InjectionType.Result]));
            }

            tmpBoxVars.Do(tmpBoxVar =>
            {
                outFlow.Append(Instruction.Create(originalDef.IsStatic ? OpCodes.Ldarg_0 : OpCodes.Ldarg_1));
                outFlow.Append(Instruction.Create(OpCodes.Ldloc, tmpBoxVar.Key));
                outFlow.Append(Instruction.Create(OpCodes.Unbox_Any, tmpBoxVar.Value));
                outFlow.Append(Instruction.Create(OpCodes.Stobj, tmpBoxVar.Value));
            });

            var returnType = fix.Method!.ReturnType;
            if (!EqualTypeRef(returnType, ts.Void))
            {
                if (!EqualTypeRef(returnType, ts.Boolean))
                    throw new Exception(
                        $"Prefix patch {fix} has not \"bool\" or \"void\" return type: {returnType}");
                outFlow.Append(Instruction.Create(OpCodes.Stloc, original.RunOriginalVariable));
            }

            if (skipLabel != null)
            {
                var instr = Instruction.Create(OpCodes.Nop);
                outFlow.AddLabel(instr, skipLabel.Value);
                outFlow.Append(instr);
            }
        }
    }

    private bool AddPostfixes(CecilFlowHelper outFlow, bool passthroughPatches, OriginalMethod original, IEnumerable<CompileTimePreludeMethod> postfixes)
    {
        var originalDef = original.MethodDef;
        var ts = originalDef.Module.TypeSystem;

        var result = false;
        var originalIsStatic = originalDef.IsStatic;
        foreach (var fix in postfixes.Where(fix => passthroughPatches == !EqualTypeRef(fix.Method!.ReturnType, ts.Void)))
        {
            var tmpBoxVars = new List<KeyValuePair<VariableDefinition, TypeReference>>();
            outFlow.AppendAll(
                EmitCallParameterAndCall(
                    outFlow,
                    original,
                    fix,
                    true,
                    fix.StaticFieldInstance,
                    out var tmpInstanceBoxingVar,
                    out var tmpObjectVar,
                    out var refResultUsed,
                    tmpBoxVars
                )
            );
            if (OriginalParameters(fix.Method!).Any(pair => pair.realName == MethodPatcherTools.ARGS_ARRAY_VAR))
                outFlow.AppendAll(RestoreArgumentArray(original));
            if (tmpInstanceBoxingVar != null)
            {
                outFlow.Append(Instruction.Create(OpCodes.Ldarg_0));
                outFlow.Append(Instruction.Create(OpCodes.Ldloc, tmpInstanceBoxingVar));
                outFlow.Append(Instruction.Create(OpCodes.Unbox_Any, originalDef.DeclaringType));
                outFlow.Append(Instruction.Create(OpCodes.Stobj, originalDef.DeclaringType));
            }

            if (refResultUsed)
            {
                var label = outFlow.DefineLabel();
                outFlow.Append(Instruction.Create(OpCodes.Ldloc, original.InjectedLocals[InjectionType.ResultRef]));
                outFlow.Append(Instruction.Create(OpCodes.Brfalse_S, label.Instruction));

                outFlow.Append(Instruction.Create(OpCodes.Ldloc, original.InjectedLocals[InjectionType.ResultRef]));
                outFlow.Append(Instruction.Create(OpCodes.Callvirt, CompileTimeAccessTools.Method(original.InjectedLocals[InjectionType.ResultRef].VariableType, "Invoke")));
                outFlow.Append(Instruction.Create(OpCodes.Stloc, original.InjectedLocals[InjectionType.Result]));
                outFlow.Append(Instruction.Create(OpCodes.Ldnull));
                outFlow.Append(Instruction.Create(OpCodes.Stloc, original.InjectedLocals[InjectionType.ResultRef]));

                var instr = Instruction.Create(OpCodes.Nop);
                outFlow.AddLabel(instr, label);
                outFlow.Append(instr);
            }
            else if (tmpObjectVar != null)
            {
                outFlow.Append(Instruction.Create(OpCodes.Ldloc, tmpObjectVar));
                outFlow.Append(Instruction.Create(OpCodes.Unbox_Any, GetReturnedType(originalDef)));
                outFlow.Append(Instruction.Create(OpCodes.Stloc, original.InjectedLocals[InjectionType.Result]));
            }

            tmpBoxVars.Do(tmpBoxVar =>
            {
                outFlow.Append(Instruction.Create(originalIsStatic ? OpCodes.Ldarg_0 : OpCodes.Ldarg_1));
                outFlow.Append(Instruction.Create(OpCodes.Ldloc, tmpBoxVar.Key));
                outFlow.Append(Instruction.Create(OpCodes.Unbox_Any, tmpBoxVar.Value));
                outFlow.Append(Instruction.Create(OpCodes.Stobj, tmpBoxVar.Value));
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

    private bool AddFinalizers(CecilFlowHelper outFlow, bool catchExceptions, OriginalMethod original, IEnumerable<CompileTimePreludeMethod> finalizers)
    {
        var originalDef = original.MethodDef;
        var module = originalDef.Module;
        var ts = module.TypeSystem;

        var rethrowPossible = true;
        var originalIsStatic = originalDef.IsStatic;
        finalizers.Do(fix =>
        {
            if (catchExceptions)
            {
                outFlow.Append(MarkBlock(outFlow, ExceptionBlockType.BeginExceptionBlock, module));
            }

            var tmpBoxVars = new List<KeyValuePair<VariableDefinition, TypeReference>>();
            outFlow.AppendAll(
                EmitCallParameterAndCall(
                    outFlow,
                    original,
                    fix,
                    false,
                    fix.StaticFieldInstance,
                    out var tmpInstanceBoxingVar,
                    out var tmpObjectVar,
                    out var refResultUsed,
                    tmpBoxVars
                )
            );
            if (OriginalParameters(fix.Method!).Any(pair => pair.realName == MethodPatcherTools.ARGS_ARRAY_VAR))
                outFlow.AppendAll(RestoreArgumentArray(original));
            if (tmpInstanceBoxingVar != null)
            {
                outFlow.Append(Instruction.Create(OpCodes.Ldarg_0));
                outFlow.Append(Instruction.Create(OpCodes.Ldloc, tmpInstanceBoxingVar));
                outFlow.Append(Instruction.Create(OpCodes.Unbox_Any, originalDef.DeclaringType));
                outFlow.Append(Instruction.Create(OpCodes.Stobj, originalDef.DeclaringType));
            }

            if (refResultUsed)
            {
                var label = outFlow.DefineLabel();
                outFlow.Append(Instruction.Create(OpCodes.Ldloc, original.InjectedLocals[InjectionType.ResultRef]));
                outFlow.Append(Instruction.Create(OpCodes.Brfalse_S, label.Instruction));

                outFlow.Append(Instruction.Create(OpCodes.Ldloc, original.InjectedLocals[InjectionType.ResultRef]));
                outFlow.Append(Instruction.Create(OpCodes.Callvirt, CompileTimeAccessTools.Method(original.InjectedLocals[InjectionType.ResultRef].VariableType, "Invoke")));
                outFlow.Append(Instruction.Create(OpCodes.Stloc, original.InjectedLocals[InjectionType.Result]));
                outFlow.Append(Instruction.Create(OpCodes.Ldnull));
                outFlow.Append(Instruction.Create(OpCodes.Stloc, original.InjectedLocals[InjectionType.ResultRef]));

                var instr = Instruction.Create(OpCodes.Nop);
                outFlow.AddLabel(instr, label);
                outFlow.Append(instr);
            }
            else if (tmpObjectVar != null)
            {
                outFlow.Append(Instruction.Create(OpCodes.Ldloc, tmpObjectVar));
                outFlow.Append(Instruction.Create(OpCodes.Unbox_Any, GetReturnedType(originalDef)));
                outFlow.Append(Instruction.Create(OpCodes.Stloc, original.InjectedLocals[InjectionType.Result]));
            }

            tmpBoxVars.Do(tmpBoxVar =>
            {
                outFlow.Append(Instruction.Create(originalIsStatic ? OpCodes.Ldarg_0 : OpCodes.Ldarg_1));
                outFlow.Append(Instruction.Create(OpCodes.Ldloc, tmpBoxVar.Key));
                outFlow.Append(Instruction.Create(OpCodes.Unbox_Any, tmpBoxVar.Value));
                outFlow.Append(Instruction.Create(OpCodes.Stobj, tmpBoxVar.Value));
            });

            if (!EqualTypeRef(fix.Method!.ReturnType, ts.Void))
            {
                outFlow.Append(Instruction.Create(OpCodes.Stloc, original.InjectedLocals[InjectionType.Exception]));
                rethrowPossible = false;
            }

            if (catchExceptions)
            {
                outFlow.Append(MarkBlock(outFlow, ExceptionBlockType.BeginCatchBlock, module));
                outFlow.Append(Instruction.Create(OpCodes.Pop));
                outFlow.Append(MarkBlock(outFlow, ExceptionBlockType.EndExceptionBlock, module));
            }
        });

        return rethrowPossible;
    }

    #endregion

    #region Codegen snippets

    private List<Instruction> PrepareArgumentArray(MethodDefinition originalDef)
    {
        var result = new List<Instruction>();

        var module = originalDef.Module;
        var parameters = originalDef.Parameters;
        foreach (var pInfo in parameters)
        {
            if (pInfo.IsOut || pInfo.Attributes.HasFlag(ParameterAttributes.Retval))
                result.AddRange(InitializeOutParameter(pInfo, pInfo.ParameterType, module));
        }

        result.Add(Instruction.Create(OpCodes.Ldc_I4, parameters.Count));
        result.Add(Instruction.Create(OpCodes.Newarr, module.ImportReference(typeof(object))));
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
                    result.Add(LoadIndOpCodeFor(pType, module));
            }

            if (pType.IsValueType)
                result.Add(Instruction.Create(OpCodes.Box, pType));
            result.Add(Instruction.Create(OpCodes.Stelem_Ref));
        }

        return result;
    }

    private List<Instruction> GenerateVariableInit(VariableDefinition variableDef, bool isReturnValue, ModuleDefinition module)
    {
        var result = new List<Instruction>();

        var ts = module.TypeSystem;
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

    private List<Instruction> InitializeOutParameter(ParameterDefinition paramDef, TypeReference typeRef, ModuleDefinition module)
    {
        var result = new List<Instruction>();

        var ts = module.TypeSystem;
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

    private List<Instruction> RestoreArgumentArray(OriginalMethod original)
    {
        var result = new List<Instruction>();

        var originalDef = original.MethodDef;
        var module = originalDef.Module;
        var parameters = originalDef.Parameters;
        var arrayIdx = 0;
        foreach (var pInfo in parameters)
        {
            var pType = pInfo.ParameterType;
            if (pType.IsByReference)
            {
                pType = pType.GetElementType();

                result.Add(Instruction.Create(OpCodes.Ldarg_S, pInfo));
                result.Add(Instruction.Create(OpCodes.Ldloc, original.InjectedLocals[InjectionType.ArgsArray]));
                result.Add(Instruction.Create(OpCodes.Ldc_I4, arrayIdx));
                result.Add(Instruction.Create(OpCodes.Ldelem_Ref));

                if (pType.IsValueType)
                {
                    result.Add(Instruction.Create(OpCodes.Unbox_Any, pType));
                    if (IsStruct(pType))
                        result.Add(Instruction.Create(OpCodes.Stobj, pType));
                    else
                        result.Add(StoreIndOpCodeFor(pType, module));
                }
                else
                {
                    result.Add(Instruction.Create(OpCodes.Castclass, pType));
                    result.Add(Instruction.Create(OpCodes.Stind_Ref));
                }
            }
            else
            {
                result.Add(Instruction.Create(OpCodes.Ldloc, original.InjectedLocals[InjectionType.ArgsArray]));
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

    private HashSet<TypeReference> GetPrimitivesWithObjectTypeCode(TypeSystem ts)
        => [ts.IntPtr, ts.UIntPtr];

    private Instruction LoadIndOpCodeFor(TypeReference typeRef, ModuleDefinition module)
    {
        var ts = module.TypeSystem;

        if (GetPrimitivesWithObjectTypeCode(ts).Any(x => EqualTypeRef(x, typeRef)))
            return Instruction.Create(OpCodes.Ldind_I);

        var dateTimeType = module.ImportReference(typeof(DateTime));
        var decimalType = module.ImportReference(typeof(decimal));
        var emptyType = module.ImportReference(typeof(void));
        var dbNullType = module.ImportReference(typeof(DBNull));

        return typeRef switch
        {
            _ when EqualTypeRef(typeRef, ts.SByte) || EqualTypeRef(typeRef, ts.Byte) || EqualTypeRef(typeRef, ts.Boolean) => Instruction.Create(OpCodes.Ldind_I1),
            _ when EqualTypeRef(typeRef, ts.Char) || EqualTypeRef(typeRef, ts.Int16) || EqualTypeRef(typeRef, ts.UInt16) => Instruction.Create(OpCodes.Ldind_I2),
            _ when EqualTypeRef(typeRef, ts.Int32) || EqualTypeRef(typeRef, ts.UInt32) => Instruction.Create(OpCodes.Ldind_I4),
            _ when EqualTypeRef(typeRef, ts.Int64) || EqualTypeRef(typeRef, ts.UInt64) => Instruction.Create(OpCodes.Ldind_I8),
            _ when EqualTypeRef(typeRef, ts.Single) => Instruction.Create(OpCodes.Ldind_R4),
            _ when EqualTypeRef(typeRef, ts.Double) => Instruction.Create(OpCodes.Ldind_R8),
            _ when EqualTypeRef(typeRef, dateTimeType) || EqualTypeRef(typeRef, decimalType) => throw new NotSupportedException(),
            _ when EqualTypeRef(typeRef, emptyType) || EqualTypeRef(typeRef, ts.Object) || EqualTypeRef(typeRef, dbNullType) || EqualTypeRef(typeRef, ts.String) => Instruction.Create(OpCodes.Ldind_Ref),
            _ => Instruction.Create(OpCodes.Ldind_Ref),
        };
    }

    Instruction StoreIndOpCodeFor(TypeReference typeRef, ModuleDefinition module)
    {
        var ts = module.TypeSystem;

        if (GetPrimitivesWithObjectTypeCode(ts).Contains(typeRef))
            return Instruction.Create(OpCodes.Stind_I);

        var dateTimeType = module.ImportReference(typeof(DateTime));
        var decimalType = module.ImportReference(typeof(decimal));
        var emptyType = module.ImportReference(typeof(void));
        var dbNullType = module.ImportReference(typeof(DBNull));

        return typeRef switch
        {
            _ when EqualTypeRef(typeRef, ts.SByte) || EqualTypeRef(typeRef, ts.Byte) || EqualTypeRef(typeRef, ts.Boolean) => Instruction.Create(OpCodes.Stind_I1),
            _ when EqualTypeRef(typeRef, ts.Char) || EqualTypeRef(typeRef, ts.Int16) || EqualTypeRef(typeRef, ts.UInt16) => Instruction.Create(OpCodes.Stind_I2),
            _ when EqualTypeRef(typeRef, ts.Int32) || EqualTypeRef(typeRef, ts.UInt32) => Instruction.Create(OpCodes.Stind_I4),
            _ when EqualTypeRef(typeRef, ts.Int64) || EqualTypeRef(typeRef, ts.UInt64) => Instruction.Create(OpCodes.Stind_I8),
            _ when EqualTypeRef(typeRef, ts.Single) => Instruction.Create(OpCodes.Stind_R4),
            _ when EqualTypeRef(typeRef, ts.Double) => Instruction.Create(OpCodes.Stind_R8),
            _ when EqualTypeRef(typeRef, dateTimeType) || EqualTypeRef(typeRef, decimalType) => throw new NotSupportedException(),
            _ when EqualTypeRef(typeRef, emptyType) || EqualTypeRef(typeRef, ts.Object) || EqualTypeRef(typeRef, dbNullType) || EqualTypeRef(typeRef, ts.String) => Instruction.Create(OpCodes.Stind_Ref),
            _ => Instruction.Create(OpCodes.Stind_Ref),
        };
    }

    private readonly MethodInfo _getMethodFromHandle1 = typeof(MethodBase).GetMethod("GetMethodFromHandle", [typeof(RuntimeMethodHandle)])!;
    private readonly MethodInfo _getMethodFromHandle2 = typeof(MethodBase).GetMethod("GetMethodFromHandle", [typeof(RuntimeMethodHandle), typeof(RuntimeTypeHandle)])!;

    bool EmitOriginalBaseMethod(MethodDefinition original, List<Instruction> result)
    {
        result.Add(Instruction.Create(OpCodes.Ldtoken, original));

        var type = original.DeclaringType;
        var module = original.Module;
        if (type.IsGenericInstance)
            result.Add(Instruction.Create(OpCodes.Ldtoken, type));
        result.Add(Instruction.Create(OpCodes.Call, module.ImportReference(type.IsGenericInstance ? _getMethodFromHandle2 : _getMethodFromHandle1)));
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

    bool IsVoid(TypeReference typeRef)
    {
        var ts = typeRef.Module.TypeSystem;
        return EqualTypeRef(typeRef, ts.Void);
    }

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

    private List<Instruction> EmitCallParameterAndCall(
        CecilFlowHelper flow,
        OriginalMethod original,
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

        var originalDef = original.MethodDef;
        var module = originalDef.Module;
        var ts = module.TypeSystem;
        var originalIsStatic = originalDef.IsStatic;
        var returnType = originalDef.ReturnType;
        var patchInjections = original.Injections[patch].ToList();

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

        CecilLabel skipInvokeLabel = default;
        if (staticFieldThis != null)
        {
            skipInvokeLabel = flow.DefineLabel();
            var invokeLabel = flow.DefineLabel();
            result.Add(Instruction.Create(OpCodes.Ldsfld, staticFieldThis));
            result.Add(Instruction.Create(OpCodes.Dup));
            result.Add(Instruction.Create(OpCodes.Brtrue_S, invokeLabel.Instruction));
            result.Add(Instruction.Create(OpCodes.Pop));
            var exceptionType = module.ImportReference(typeof(Exception));
            if (EqualTypeRef(patch.Method!.ReturnType, ts.Boolean))
            {
                result.Add(Instruction.Create(OpCodes.Ldc_I4_1));
            }
            else if (EqualTypeRef(patch.Method!.ReturnType, exceptionType))
            {
                result.Add(Instruction.Create(OpCodes.Ldloc, original.ExceptionVariable!));
            }
            else if (!EqualTypeRef(patch.Method!.ReturnType, ts.Void))
            {
                throw new Exception($"Static field instance patch {patch} must have a \"bool\", \"Exception\" or \"void\" return type");
            }

            result.Add(Instruction.Create(OpCodes.Br_S, skipInvokeLabel.Instruction));
            result.Add(NopWithLabels(flow, invokeLabel));
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
                if (original.ExceptionVariable != null)
                    result.Add(Instruction.Create(OpCodes.Ldloc, original.ExceptionVariable));
                else
                    result.Add(Instruction.Create(OpCodes.Ldnull));
                continue;
            }

            if (injectionType == InjectionType.RunOriginal)
            {
                if (original.RunOriginalVariable != null)
                    result.Add(Instruction.Create(OpCodes.Ldloc, original.RunOriginalVariable));
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
                if (original.InjectedLocals.TryGetValue(InjectionType.ArgsArray, out var argsArrayVar))
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
                var stateVarName = $"__state_{original.MethodDef.Name}"; // TODO: This will fail if there are multiple patches on the same method that use state

                if (original.OtherLocals.TryGetValue(stateVarName, out var stateVar))
                    result.Add(Instruction.Create(ldlocCode, stateVar));
                else
                {
                    logger.LogError($"State variable '{stateVarName}' not found in locals for patched method '{patch.MethodName}'");
                    result.Add(Instruction.Create(OpCodes.Ldnull));
                }

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
                result.Add(Instruction.Create(ldlocCode, original.InjectedLocals[InjectionType.Result]));
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

                result.Add(Instruction.Create(OpCodes.Ldloca, original.InjectedLocals[InjectionType.ResultRef]));

                refResultUsed = true;
                continue;
            }

            if (original.OtherLocals.TryGetValue(paramRealName, out var localBuilder))
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
                if (isInstance)
                    argumentIdx++;
                if (argumentIdx < 0 || argumentIdx >= originalParameters.Count)
                    throw new Exception($"No parameter found at index {argumentIdx}");
            }
            else
            {
                argumentIdx = GetArgumentIndex(patch.Method!.Resolve(), originalParameterNames, injection.ParameterDef);
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

            var patchArgIndex = argumentIdx + (isInstance && staticFieldThis == null ? 1 : 0);
            var originalParamType = originalParameters[argumentIdx].ParameterType;
            var originalParamElementType = originalParamType.IsByReference ? originalParamType.GetElementType() : originalParamType;
            var patchParamType = paramType;
            var patchParamElementType = patchParamType.IsByReference ? patchParamType.GetElementType() : patchParamType;
            var originalIsNormal = originalParameters[argumentIdx].IsOut is false && originalParamType.IsByReference is false;
            var patchIsNormal = injection.ParameterDef.IsOut is false && patchParamType.IsByReference is false;
            var needsBoxing = originalParamElementType.IsValueType && patchParamElementType.IsValueType is false;

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
                    result.Add(LoadIndOpCodeFor(originalParameters[argumentIdx].ParameterType, module));
            }
        }

        result.Add(Instruction.Create(OpCodes.Call, patch.Method));

        if (staticFieldThis != null)
        {
            result.Add(NopWithLabels(flow, skipInvokeLabel));
        }

        return result;
    }

    private IEnumerable<(ParameterDefinition info, string realName)> OriginalParameters(MethodReference methodRef)
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

    private bool AffectsOriginal(OriginalMethod original, CompileTimePreludeMethod fix)
    {
        var ts = original.MethodDef.Module.TypeSystem;

        if (EqualTypeRef(fix.Method!.ReturnType, ts.Boolean))
            return true;

        if (original.Injections.TryGetValue(fix, out var injectedParameters) == false)
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

    CecilFlowHelper FaultRewrite(CecilFlowHelper inFlow, ModuleDefinition module)
    {
        var ts = module.TypeSystem;
        var originalInstructions = inFlow.Instructions;

        var outFlow = new CecilFlowHelper();

        var i = 0;
        while (i < originalInstructions.Count)
        {
            var cur = originalInstructions[i];

            if (!HasBlock(inFlow, cur, ExceptionBlockType.BeginFaultBlock))
            {
                var newInstr = CopyInstrWithFlow(outFlow, inFlow, cur);
                outFlow.Append(newInstr);
                ++i;
                continue;
            }

            var beginExceptionIdx = FindMatchingBeginException(outFlow);
            var endExceptionIdx = FindMatchingEndException(inFlow, i + 1);

            if (beginExceptionIdx < 0 || endExceptionIdx < 0)
                throw new InvalidOperationException("Unbalanced exception markers – cannot rewrite.");

            // var faultBody = new List<Instruction>();
            // for (var k = i; k < endExceptionIdx; ++k)
            //  faultBody.Add(CloneWithoutFaultMarker(outFlow, inFlow, originalInstructions[k]));

            i = endExceptionIdx + 1;

            var failedLocal = new VariableDefinition(ts.Boolean);
            var skipFault = outFlow.DefineLabel();

            var excTypeRef = module.ImportReference(typeof(Exception));
            outFlow.Append(NopWithBlocks(outFlow, new CecilExceptionBlock(ExceptionBlockType.BeginCatchBlock, excTypeRef)));
            outFlow.Append(Instruction.Create(OpCodes.Pop));
            outFlow.Append(Instruction.Create(OpCodes.Ldc_I4_1));
            outFlow.Append(Instruction.Create(OpCodes.Stloc, failedLocal.Index));
            outFlow.Append(Instruction.Create(OpCodes.Rethrow));
            outFlow.Append(NopWithBlocks(outFlow, new CecilExceptionBlock(ExceptionBlockType.BeginFinallyBlock)));
            outFlow.Append(Instruction.Create(OpCodes.Ldloc, failedLocal.Index));
            outFlow.Append(Instruction.Create(OpCodes.Brfalse_S, skipFault.Instruction));
            outFlow.Append(NopWithLabels(outFlow, skipFault));
            outFlow.Append(NopWithBlocks(outFlow, new CecilExceptionBlock(ExceptionBlockType.EndExceptionBlock)));
        }

        return outFlow;
    }

    Instruction CloneWithoutFaultMarker(CecilFlowHelper outFlow, CecilFlowHelper inFlow, Instruction instr)
    {
        var copy = Instruction.Create(instr.OpCode);
        copy.Operand = instr.Operand;
        if (inFlow.TryGetLabels(instr, out var instrLabels))
            outFlow.AddLabels(copy, [..instrLabels]);
        if (inFlow.TryGetBlocks(instr, out var instrBlocks))
            outFlow.AddBlocks(copy, [..instrBlocks.Where(b => b.BlockType != ExceptionBlockType.BeginFaultBlock)]);
        return copy;
    }

    int FindMatchingBeginException(CecilFlowHelper flow)
    {
        var instructions = flow.Instructions;
        for (int j = instructions.Count - 1, depth = 0; j >= 0; --j)
        {
            if (HasBlock(flow, instructions[j], ExceptionBlockType.EndExceptionBlock)) ++depth;
            if (HasBlock(flow, instructions[j], ExceptionBlockType.BeginExceptionBlock))
            {
                if (depth == 0) return j;
                --depth;
            }
        }

        return -1;
    }

    int FindMatchingEndException(CecilFlowHelper flow, int start)
    {
        var instructions = flow.Instructions;
        for (int j = start, depth = 0; j < instructions.Count; ++j)
        {
            if (HasBlock(flow, instructions[j], ExceptionBlockType.BeginExceptionBlock)) ++depth;
            if (HasBlock(flow, instructions[j], ExceptionBlockType.EndExceptionBlock))
            {
                if (depth == 0) return j;
                --depth;
            }
        }

        return -1;
    }

    private Instruction CopyInstrWithoutFlow(Instruction instr)
        => instr.GetPrototype();

    private Instruction CopyInstrWithFlow(CecilFlowHelper outFlow, CecilFlowHelper inFlow, Instruction instr)
    {
        var newInstr = instr.GetPrototype();

        if (inFlow.TryGetBlocks(instr, out var instrBlocks))
            outFlow.AddBlocks(newInstr, [..instrBlocks]);
        if (inFlow.TryGetLabels(instr, out var instrLabels))
            outFlow.AddLabels(newInstr, [..instrLabels]);
        return newInstr;
    }

    #endregion

    #region Block

    private Instruction MarkBlock(CecilFlowHelper flow, ExceptionBlockType blockType, ModuleDefinition module)
    {
        var instr = Instruction.Create(OpCodes.Nop);
        var excTypeRef = module.ImportReference(typeof(Exception));
        flow.AddBlock(instr, new CecilExceptionBlock(blockType, excTypeRef));
        return instr;
    }

    bool HasBlock(CecilFlowHelper flow, Instruction instr, ExceptionBlockType type)
        => flow.TryGetBlocks(instr, out var instrBlocks) && instrBlocks.Any(block => block.BlockType == type);

    Instruction WithBlocks(CecilFlowHelper flow, Instruction instr, params CecilExceptionBlock[] blocks)
    {
        flow.AddBlocks(instr, blocks);
        return instr;
    }

    Instruction NopWithBlocks(CecilFlowHelper flow, params CecilExceptionBlock[] instrBlocks)
    {
        var nop = Instruction.Create(OpCodes.Nop);
        return WithBlocks(flow, nop, instrBlocks);
    }

    #endregion

    #region Labels

    private Instruction WithLabels(CecilFlowHelper flow, Instruction instr, params CecilLabel[] labels)
    {
        flow.AddLabels(instr, labels);
        return instr;
    }

    private Instruction NopWithLabels(CecilFlowHelper flow, params CecilLabel[] labels)
    {
        var nop = Instruction.Create(OpCodes.Nop);
        return WithLabels(flow, nop, labels);
    }

    private Instruction NopWithLabels(CecilFlowHelper flow, List<CecilLabel> labels)
    {
        var instr = Instruction.Create(OpCodes.Nop);
        flow.AddLabels(instr, labels);
        return instr;
    }

    #endregion

    private bool AnyFixHas(OriginalMethod original, InjectionType type)
        => original.Injections.Values.SelectMany(list => list).Any(pair => pair.InjectionType == type);

    private IEnumerable<InjectedParameter> InjectionsFor(OriginalMethod original, CompileTimePreludeMethod fix, InjectionType type = InjectionType.Unknown)
    {
        if (original.Injections.TryGetValue(fix, out var list))
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