using Mono.Cecil;
using Mono.Cecil.Cil;

namespace PreludeLib.CompileTime.Backend;

public class MethodBodyRestoreHelper
{
    private struct InstructionRestoreEntry
    {
        public Instruction Instruction;
        public object Operand;
    }

    private struct ExceptionHandlerRestoreEntry
    {
        public ExceptionHandler ExceptionHandler;
        public Instruction TryStart;
        public Instruction TryEnd;
        public Instruction HandlerStart;
        public Instruction HandlerEnd;
        public Instruction FilterStart;
        public TypeReference CatchType;
        public ExceptionHandlerType HandlerType;
    }
	
    private struct MethodBodyRestoreEntry
    {
        public List<InstructionRestoreEntry> Instructions;
        public List<ExceptionHandlerRestoreEntry> ExceptionHandlers;
        public bool InitLocals;
        public List<VariableDefinition> Variables;
        public int MaxStackSize;
    }
    private readonly Dictionary<MethodDefinition, MethodBodyRestoreEntry> _originalBodies = [];
    
    public void SaveAndOverride(MethodDefinition methodDef)
    {
        if (!_originalBodies.TryGetValue(methodDef, out var entry))
        {
            entry = new MethodBodyRestoreEntry();
            _originalBodies.Add(methodDef, entry);
        }
        
        Save(ref entry, methodDef.Body);
        _originalBodies[methodDef] = entry;
    }
    
    public void SaveIfNotSaved(MethodDefinition methodDef)
    {
        if (!_originalBodies.ContainsKey(methodDef))
        {
            var entry = new MethodBodyRestoreEntry();
            Save(ref entry, methodDef.Body);
            _originalBodies.Add(methodDef, entry);
        }
    }

    public void Restore(MethodDefinition methodDef)
    {
        if (_originalBodies.TryGetValue(methodDef, out var entry))
        {
            RestoreMethodBody(methodDef.Body, entry);
            _originalBodies.Remove(methodDef);
        }
    }
    
    private void Save(ref MethodBodyRestoreEntry entry, MethodBody body)
    {
        entry.InitLocals = body.InitLocals;
        entry.ExceptionHandlers = body.ExceptionHandlers.Select(x => new ExceptionHandlerRestoreEntry()
        {
            ExceptionHandler = x,
            TryStart = x.TryStart,
            TryEnd = x.TryEnd,
            HandlerStart = x.HandlerStart,
            HandlerEnd = x.HandlerEnd,
            FilterStart = x.FilterStart,
            CatchType = x.CatchType,
            HandlerType = x.HandlerType,
        }).ToList();
        entry.MaxStackSize = body.MaxStackSize;
        entry.Variables = body.Variables.ToList();
        entry.Instructions = body.Instructions.Select(x =>
        {
            var operand = x.Operand;
            if (operand is Array array)
            {
                operand = array.Clone();
            }
            return new InstructionRestoreEntry()
            {
                Instruction = x,
                Operand = x.Operand,
            };
        }).ToList();
    }

    private void RestoreMethodBody(MethodBody body, MethodBodyRestoreEntry entry)
    {
        body.InitLocals = entry.InitLocals;
        body.MaxStackSize = entry.MaxStackSize;
	    
        body.Variables.Clear();
        foreach (var variable in entry.Variables)
            body.Variables.Add(variable);
	    
        body.Instructions.Clear();
        foreach (var instrEntry in entry.Instructions)
        {
            var instr = instrEntry.Instruction;
            instr.Operand = instrEntry.Operand;
            body.Instructions.Add(instr);
        }

        body.ExceptionHandlers.Clear();
        foreach (var excEntry in entry.ExceptionHandlers)
        {
            var exc = new ExceptionHandler(excEntry.HandlerType)
            {
                TryStart = excEntry.TryStart,
                TryEnd = excEntry.TryEnd,
                HandlerStart = excEntry.HandlerStart,
                HandlerEnd = excEntry.HandlerEnd,
                FilterStart = excEntry.FilterStart,
                CatchType = excEntry.CatchType,
            };
            body.ExceptionHandlers.Add(exc);
        }
    }
}