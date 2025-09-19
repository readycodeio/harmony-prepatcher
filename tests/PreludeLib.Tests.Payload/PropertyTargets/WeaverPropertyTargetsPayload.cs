using Microsoft.Extensions.Logging;
using PreludeLib.Runtime.Backend;
using PreludeLib.Runtime.Backend.WeaverCallback;

namespace PreludeLib.Payload.PropertyTargets;

public class WeaverPropertyTargetsPayload(ILogger logger) : PropertyTargetsPayloadBase(true, logger)
{
    protected override IRuntimeBackend CreateBackend()
        => new RuntimeWeaverBackend(Logger);
}