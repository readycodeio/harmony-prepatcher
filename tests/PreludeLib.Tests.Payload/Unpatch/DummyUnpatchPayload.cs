using Microsoft.Extensions.Logging;
using PreludeLib.Runtime;
using PreludeLib.Runtime.Backend;
using PreludeLib.Runtime.Backend.Dummy;

namespace PreludeLib.Payload.Unpatch;

public class DummyUnpatchPayload(ILogger logger) : UnpatchPayloadBase(false, logger)
{
    protected override IRuntimeBackend CreateBackend()
        => new RuntimeDummyBackend();
}