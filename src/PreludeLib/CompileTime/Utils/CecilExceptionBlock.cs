using HarmonyLib;
using Mono.Cecil;

namespace PreludeLib.CompileTime.Utils;

public class CecilExceptionBlock
{
    public ExceptionBlockType BlockType;
    public TypeReference? CatchType;

    public CecilExceptionBlock(ExceptionBlockType blockType, TypeReference catchType)
    {
        this.BlockType = blockType;
        this.CatchType = catchType;
    }
    
    public CecilExceptionBlock(ExceptionBlockType blockType)
    {
        this.BlockType = blockType;
        this.CatchType = null;
    }
}