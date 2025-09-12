using Microsoft.Extensions.Logging;
using PreludeLib.Runtime;
using PreludeLib.Runtime.WeaverCallback;

namespace PreludeLib.Payload.Simple;

public class WeaverSimplePayload(bool shouldPass, ILogger logger) : SimplePayloadBase(shouldPass, logger)
{
    protected override IPreludeBackend CreateBackend(string id)
        => new PreludeWeaverBackend(id);
}