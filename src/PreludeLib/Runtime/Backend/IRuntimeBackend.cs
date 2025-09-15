using PreludeLib.Runtime.Registry;

namespace PreludeLib.Runtime.Backend;

public interface IRuntimeBackend
{
    void Commit(IRuntimePatchRegistry registry);
}
