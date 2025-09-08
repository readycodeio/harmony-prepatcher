namespace PreludeLib.Tests.Examples;

public static class FinalizerProbes
{
    public static bool FinalizerRan;
    public static Exception? LastException;

    public static void Reset()
    {
        FinalizerRan = false;
        LastException = null;
    }
}