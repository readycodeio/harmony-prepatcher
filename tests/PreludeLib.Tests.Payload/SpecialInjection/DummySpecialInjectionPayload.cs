 using Microsoft.Extensions.Logging;
 using PreludeLib.Runtime;
using PreludeLib.Runtime.Backend.Dummy;

namespace PreludeLib.Payload.SpecialInjection;

public class DummySpecialInjectionPayload(ILogger logger) : SpecialInjectionPayloadBase(false, logger)
{
    protected override IPreludeBackend CreateBackend(string id)
        => new PreludeDummyBackend(id);
}