namespace PreludeLib.Tests.Examples;

public static class TargetingProbes
{
    public static void Reset()
    {
        Over2PostfixHit = false;
        GetterPostfixHit = false;
        CtorIntPostfixHit = false;
        CtorSeenBaseVal = 0;
    }

    public static bool Over2PostfixHit;
    public static bool GetterPostfixHit;
    public static bool CtorIntPostfixHit;
    public static int CtorSeenBaseVal;
}