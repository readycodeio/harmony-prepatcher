using Microsoft.Extensions.Logging;
using PreludeLib.Runtime.Backend;
using PreludeLib.Runtime.Public;

namespace PreludeLib.Payload;

public abstract class BackendPayloadBase(bool shouldPass, ILogger logger)
{
    public bool ShouldPass
        => shouldPass;
    
    public ILogger Logger
        => logger;
    
    protected string GenerateId(string baseName)
    {
        if (baseName.EndsWith("_Payload"))
            baseName = baseName[..^"_Payload".Length];
        return $"test-{baseName}-{Guid.NewGuid():N}";
    }

    protected abstract IRuntimeBackend CreateBackend();

    private IRuntimeBackend? _backend;
    private RuntimePrelude? _prelude;
    
    private IRuntimeBackend GetOrCreateBackend()
        => _backend ??= CreateBackend();
    
    private RuntimePrelude CreatePrelude()
        => new(GetOrCreateBackend());
    
    protected RuntimePrelude GetOrCreatePrelude()
        => _prelude ??= CreatePrelude();

    protected RuntimePreludeBuilder CreateBuilder(string id)
        => GetOrCreatePrelude().Create(id);
}