using Microsoft.Extensions.Logging;
using PreludeLib.Runtime;
using PreludeLib.Runtime.DummyBackend;

namespace PreludeLib.Payload.Finalizer;

public class DummyFinalizerPayload(ILogger logger) : FinalizerPayloadBase(false, logger)
{
    protected override IPreludeBackend CreateBackend(string id)
        => new PreludeDummyBackend(id);
}