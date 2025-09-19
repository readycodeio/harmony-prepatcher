using Microsoft.Extensions.Logging;
using PreludeLib.Runtime.Backend;
using PreludeLib.Runtime.Backend.WeaverCallback;

namespace PreludeLib.Payload.Unpatch;

public class WeaverUnpatchPayload(ILogger logger) : UnpatchPayloadBase(true, logger)
{
    protected override IRuntimeBackend CreateBackend()
        => new RuntimeWeaverBackend(Logger);
}