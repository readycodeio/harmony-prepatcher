namespace PreludeLib.Tests.Examples;

public static class SpecialInjectionProbes
{
    public static object? LastInstance;
    public static System.Reflection.MethodBase? LastOriginal;
    public static int[]? LastArgsSnapshot;

    public static void Reset()
    {
        LastInstance = null;
        LastOriginal = null;
        LastArgsSnapshot = null;
    }
}