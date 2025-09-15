using PreludeLib.Runtime.Registry;

namespace PreludeLib.Runtime.Backend.Dummy;

public class RuntimeDummyBackend() : IRuntimeBackend
{
    public void Commit(IRuntimePatchRegistry registry)
    {
        // no-op
    }
}