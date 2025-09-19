using Microsoft.Extensions.Logging;
using PreludeLib.Runtime.Backend;
using PreludeLib.Runtime.Backend.WeaverCallback;

namespace PreludeLib.Payload.Postfix;

public class WeaverPostfixPayload(ILogger logger) : PostfixPayloadBase(true, logger)
{
    protected override IRuntimeBackend CreateBackend()
        => new RuntimeWeaverBackend(Logger);
}