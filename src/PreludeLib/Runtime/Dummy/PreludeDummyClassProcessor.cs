using System.Reflection;

namespace PreludeLib.Runtime.DummyBackend;

public class PreludeDummyClassProcessor(PreludeDummyBackend instance, Type type) : IPreludeClassProcessor
{
    public string? Category { get; }

    public List<MethodInfo> Patch()
        => [];
}