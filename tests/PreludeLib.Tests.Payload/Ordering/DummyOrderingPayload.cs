using PreludeLib.Runtime;
using PreludeLib.Runtime.DummyBackend;
using Microsoft.Extensions.Logging;

namespace PreludeLib.Payload.Ordering;

public class DummyOrderingPayload(ILogger logger) : OrderingPayloadBase(false, logger)
{
    protected override IPreludeBackend CreateBackend(string id)
        => new PreludeDummyBackend(id);
}