namespace PreludeLib.Tests.Examples;

public class OverloadArgTargets
{
    // Non-ref overload for control
    public int Inc(int x) => x + 1;

    // Ref overload we will patch by signature
    public int Inc(ref int x)
    {
        x += 1;
        return x;
    }

    // Out overload we will patch by signature
    public bool TryMake(out int value)
    {
        value = 123; // predictable default
        return true;
    }

    // Control overload to prove we targeted the right one (different signature)
    public bool TryMake(int seed, out int value)
    {
        value = seed * 2;
        return true;
    }
}