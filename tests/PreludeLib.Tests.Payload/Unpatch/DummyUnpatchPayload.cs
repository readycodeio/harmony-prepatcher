using Microsoft.Extensions.Logging;
using PreludeLib.Runtime;
using PreludeLib.Runtime.DummyBackend;

namespace PreludeLib.Payload.Unpatch;

public class DummyUnpatchPayload(ILogger logger) : UnpatchPayloadBase(false, logger)
{
    protected override IPreludeBackend CreateBackend(string id)
        => new PreludeDummyBackend(id);
}