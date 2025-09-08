namespace PreludeLib.Tests.Examples;

public class PrivateFieldTargets
{
    // The private field accessed via ___secret in patches
    private int secret = 5;

    public int Bump(int x) => secret + x;

    public int GetSecret() => secret;
}