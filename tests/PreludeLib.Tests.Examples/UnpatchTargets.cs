using System.Runtime.CompilerServices;

namespace PreludeLib.Tests.Examples;

public class UnpatchTargets
{
    // Deterministic function so we can observe prefix order and result shaping
    [MethodImpl(MethodImplOptions.NoInlining)]
    public int Compute(int x) => x;
}