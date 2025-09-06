using Mono.Cecil;

namespace PreludeLib;

public class CompileTimePatchProcessor
{
    private readonly Prelude _instance;
    private readonly MethodDefinition _original;

    public CompileTimePatchProcessor(Prelude instance, MethodReference original)
        : this(instance, original.Resolve())
    {
        // empty
    }
    
    public CompileTimePatchProcessor(Prelude instance, MethodDefinition original)
    {
        _instance = instance;
        _original = original;
    }
}