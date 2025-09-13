using Microsoft.Extensions.Logging;
using PreludeLib.Runtime;
using PreludeLib.Runtime.Backend.Dummy;

namespace PreludeLib.Payload.Simple;

public class DummySimplePayload(ILogger logger) : SimplePayloadBase(false, logger)
{
    protected override IPreludeBackend CreateBackend(string id)
        => new PreludeDummyBackend(id);
}