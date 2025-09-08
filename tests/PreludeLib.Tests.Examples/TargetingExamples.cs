namespace PreludeLib.Tests.Examples;

public class TargetingExamples
{
    private readonly int _base;

    public TargetingExamples()
    {
        _base = 1;
        CtorTag = "default";
    }

    public TargetingExamples(int baseVal)
    {
        _base = baseVal;
        CtorTag = "int";
    }

    public string CtorTag { get; private set; }

    // Overloads we’ll target by name + signature
    public int Over(int x) => _base + x;
    public int Over(int x, int y) => _base + x + y;
    public int Over(ref int x)
    {
        x += _base;
        return x;
    }

    // Property we’ll target via MethodType.Getter
    private int _propBacking;
    public int Value
    {
        get => _propBacking + 100;
        set => _propBacking = value;
    }
}