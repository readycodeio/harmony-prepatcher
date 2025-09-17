using HarmonyLib;
using Mono.Cecil;

namespace PreludeLib.CompileTime.Backend.WeaverCallback;

public class CompileTimeExceptionBlock
{
    public ExceptionBlockType BlockType;
    public TypeReference? CatchType;

    public CompileTimeExceptionBlock(ExceptionBlockType blockType, TypeReference catchType)
    {
        this.BlockType = blockType;
        this.CatchType = catchType;
    }
    
    public CompileTimeExceptionBlock(ExceptionBlockType blockType)
    {
        this.BlockType = blockType;
        this.CatchType = null;
    }
}