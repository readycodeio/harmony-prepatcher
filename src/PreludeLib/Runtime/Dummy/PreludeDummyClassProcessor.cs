using System;

namespace PreludeLib.Runtime.DummyBackend;

public class PreludeDummyClassProcessor : IPreludeClassProcessor
{
    private readonly PreludeDummyBackend _instance;
    private readonly Type _type;
    
    public PreludeDummyClassProcessor(PreludeDummyBackend instance, Type type)
    {
        _instance = instance;
        _type = type;
    }

    public void Patch()
    {
        // no-op!
    }
}