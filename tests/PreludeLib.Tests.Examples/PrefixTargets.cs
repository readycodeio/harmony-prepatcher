namespace PreludeLib.Tests.Examples;

public class PrefixTargets
{
    // Simple pure method; used for skipping + alias tests
    public int Sum(int a, int b) => a + b;

    // Method with a ref argument; original does not mutate x
    public int MultiplyRef(ref int x, int factor) => x * factor;

    // Method without parameters; original sets predictable values
    public void MakePair(int seed, out int a, out int b)
    {
        a = seed;
        b = seed * 2;
    }
}