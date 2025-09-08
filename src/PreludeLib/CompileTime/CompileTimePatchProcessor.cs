using Mono.Cecil;

namespace PreludeLib.CompileTime;

public class CompileTimePatchProcessor
{
    private readonly CompileTimePrelude _instance;
    private readonly MethodDefinition _original;

    public CompileTimePatchProcessor(CompileTimePrelude instance, MethodReference original)
        : this(instance, original.Resolve())
    {
        // empty
    }
    
    public CompileTimePatchProcessor(CompileTimePrelude instance, MethodDefinition original)
    {
        _instance = instance;
        _original = original;
    }
}