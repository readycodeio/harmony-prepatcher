namespace PreludeLib.Tests.Examples;

public class FinalizerTargets
{
    // Throws on negative input, otherwise returns x*2.
    public int MightThrow(int x)
    {
        if (x < 0) throw new InvalidOperationException("neg not allowed");
        return x * 2;
    }
}