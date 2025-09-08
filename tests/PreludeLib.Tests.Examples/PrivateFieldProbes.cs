namespace PreludeLib.Tests.Examples;

public static class PrivateFieldProbes
{
    public static int PrefixSeenSecret;
    public static int PostfixSeenSecret;

    public static void Reset()
    {
        PrefixSeenSecret = int.MinValue;
        PostfixSeenSecret = int.MinValue;
    }
}