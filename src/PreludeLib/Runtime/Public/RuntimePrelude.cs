using Microsoft.Extensions.Logging;
using PreludeLib.Runtime.Backend;
using PreludeLib.Runtime.Registry;

namespace PreludeLib.Runtime.Public;

public class RuntimePrelude(IRuntimeBackend backend, ILogger logger)
{
    private readonly RuntimePatchRegistry _registry = new();

    public RuntimePreludeBuilder Create(string id)
        => new(this, id, _registry, logger);

    public void Commit()
    {
        backend.Commit(_registry);
        _registry.ResetChanges();
    }
}