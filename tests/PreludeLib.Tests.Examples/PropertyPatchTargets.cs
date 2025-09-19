namespace PreludeLib.Tests.Examples;

public class PropertyPatchTargets
{
    private int _pBacking;

    // Explicit getter/setter so we can patch the setter via MethodType.Setter
    public int P
    {
        get => _pBacking;
        set => _pBacking = value;
    }

    // Helper to assert the actual backing value
    public int P_Raw() => _pBacking;

    // Counter to observe that the setter postfix ran
    public int Counter { get; private set; }
    public void Bump() => Counter++;

    // Auto property for the "prefix modifies incoming value" test
    public int Auto { get; set; }
}