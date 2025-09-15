using Microsoft.Extensions.Logging;
using PreludeLib.Runtime.Backend;
using PreludeLib.Runtime.Backend.Dummy;

namespace PreludeLib.Payload.Simple;

public class DummySimplePayload(ILogger logger) : SimplePayloadBase(false, logger)
{
    protected override IRuntimeBackend CreateBackend()
        => new RuntimeDummyBackend();
}