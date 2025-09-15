using Microsoft.Extensions.Logging;
using PreludeLib.Runtime;
using PreludeLib.Runtime.Backend;
using PreludeLib.Runtime.Backend.Dummy;

namespace PreludeLib.Payload.OverloadArg;

public class DummyOverloadArgPayload(ILogger logger) : OverloadArgPayloadBase(false, logger)
{
    protected override IRuntimeBackend CreateBackend()
        => new RuntimeDummyBackend();
}