using Mono.Cecil.Cil;

namespace PreludeLib.CompileTime.Utils;

public readonly struct CecilLabel : IEquatable<CecilLabel>
{
    public readonly Instruction Instruction;

    internal CecilLabel(Instruction instruction)
    {
        Instruction = instruction;
    }

    public bool Equals(CecilLabel other)
        => Instruction == other.Instruction;

    public override bool Equals(object? obj)
        => obj is CecilLabel other && Equals(other);

    public override int GetHashCode()
        => Instruction.GetHashCode();
}

public class CecilFlowHelper
{
    private readonly List<Instruction> _instructions = [];
    private readonly Dictionary<Instruction, List<CecilLabel>> _labels = [];
    private readonly Dictionary<Instruction, List<CecilExceptionBlock>> _blocks = [];
    private readonly List<CecilLabel> _labelsToMark = [];
    
    public IReadOnlyList<Instruction> Instructions
        => _instructions;

    #region  Labels

    public CecilLabel DefineLabel()
    {
        var labelInstr = Instruction.Create(OpCodes.Nop);
        var label = new CecilLabel(labelInstr);
        _labelsToMark.Add(label);
        return label;
    }

    public void AddLabel(Instruction instr, CecilLabel cecilLabel)
    {
        if (!_labels.TryGetValue(instr, out var instrLabels))
        {
            instrLabels = [];
            _labels.Add(instr, instrLabels);
        }
        instrLabels.Add(cecilLabel);
    }
    
    public void AddLabels(Instruction instr, IEnumerable<CecilLabel> labels)
    {
        if (!_labels.TryGetValue(instr, out var instrLabels))
        {
            instrLabels = [];
            _labels.Add(instr, instrLabels);
        }
        instrLabels.AddRange(labels);
    }

    public bool TryGetLabels(Instruction instr, out IReadOnlyList<CecilLabel> labels)
    {
        var result = _labels.TryGetValue(instr, out var lst);
        labels = lst ?? [];
        return result;
    }

    public void CheckAllMarked()
    {
        foreach (var label in _labelsToMark)
        {
            var found = false;
            foreach (var instr in _instructions)
            {
                if (_labels.TryGetValue(instr, out var instrLabels))
                {
                    if (instrLabels.Contains(label))
                    {
                        found = true;
                        break;
                    }
                }
            }
            
            if (!found)
                throw new InvalidOperationException("A defined label was not marked on any instruction.");
        }
    }

    #endregion

    #region Blocks
    
    public void AddBlock(Instruction instr, CecilExceptionBlock block)
    {
        if (!_blocks.TryGetValue(instr, out var instrBlocks))
        {
            instrBlocks = [];
            _blocks.Add(instr, instrBlocks);
        }
        instrBlocks.Add(block);
    }

    public void AddBlocks(Instruction instr, IEnumerable<CecilExceptionBlock> blocks)
    {
        if (!_blocks.TryGetValue(instr, out var instrBlocks))
        {
            instrBlocks = [];
            _blocks.Add(instr, instrBlocks);
        }
        instrBlocks.AddRange(blocks);
    }

    public bool TryGetBlocks(Instruction instr, out IReadOnlyList<CecilExceptionBlock> blocks)
    {
        var result = _blocks.TryGetValue(instr, out var lst);
        blocks = lst ?? [];
        return result;
    }
    
    #endregion
    
    #region Instruction manipulation

    public void Append(Instruction instr)
    {
        _instructions.Add(instr);
    }
    
    public void AppendAll(IEnumerable<Instruction> instrList)
    {
        _instructions.AddRange(instrList);
    }
    
    public void AppendFlow(CecilFlowHelper flow)
    {
        _instructions.AddRange(flow._instructions);
        foreach (var (instr, labels) in flow._labels)
            AddLabels(instr, [..labels]);
        foreach (var (instr, blocks) in flow._blocks)
            AddBlocks(instr, [..blocks]);
    }
    
    public void Remove(Instruction lastInstruction)
    {
        var removed = _instructions.Remove(lastInstruction);
        if (!removed)
            throw new InvalidOperationException("The instruction to remove was not found in the instruction list.");
    }

    public void Replace(Instruction instr, Instruction newInstr)
    {
        var index = _instructions.IndexOf(instr);
        if (index < 0)
            throw new InvalidOperationException("The instruction to replace was not found in the instruction list.");
        _instructions[index] = newInstr;
        if (_labels.TryGetValue(instr, out var instrLabels))
            _labels.Add(newInstr, [..instrLabels]);
        if (_blocks.TryGetValue(instr, out var instrBlocks))
            _blocks.Add(newInstr, [..instrBlocks]);
    }
    
    #endregion
}