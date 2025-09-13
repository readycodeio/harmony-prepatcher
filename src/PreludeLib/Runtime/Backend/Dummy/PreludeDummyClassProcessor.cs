namespace PreludeLib.Runtime.Backend.Dummy;

public class PreludeDummyClassProcessor(PreludeDummyBackend instance, Type type) : IPreludeClassProcessor
{
    public string? Category { get; }

    public void Patch()
    {
        // no-op
    }
}