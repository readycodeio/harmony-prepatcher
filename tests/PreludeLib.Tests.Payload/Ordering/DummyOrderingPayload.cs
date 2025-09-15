using PreludeLib.Runtime;
using PreludeLib.Runtime.Backend.Dummy;
using Microsoft.Extensions.Logging;
using PreludeLib.Runtime.Backend;

namespace PreludeLib.Payload.Ordering;

public class DummyOrderingPayload(ILogger logger) : OrderingPayloadBase(false, logger)
{
    protected override IRuntimeBackend CreateBackend()
        => new RuntimeDummyBackend();
}