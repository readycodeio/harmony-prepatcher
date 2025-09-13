using Microsoft.Extensions.Logging;
using PreludeLib.Runtime;
using PreludeLib.Runtime.Backend.Dummy;

namespace PreludeLib.Payload.Postfix;

public class DummyPostfixPayload(ILogger logger) : PostfixPayloadBase(false, logger)
{
    protected override IPreludeBackend CreateBackend(string id)
        => new PreludeDummyBackend(id);
}