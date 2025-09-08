namespace PreludeLib.Tests.Examples;

/// Probes to observe behavior in patches (kept public for easy assertions)
public static class PostfixProbes
{
    public static bool VoidPostfixExecuted;
    public static int ObservedA;
    public static int ObservedB;

    public static void Reset()
    {
        VoidPostfixExecuted = false;
        ObservedA = 0;
        ObservedB = 0;
    }
}