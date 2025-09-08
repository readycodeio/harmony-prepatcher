using Microsoft.Extensions.Logging;
using PreludeLib.Runtime;

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

    protected abstract IPreludeBackend CreateBackend(string id);
}