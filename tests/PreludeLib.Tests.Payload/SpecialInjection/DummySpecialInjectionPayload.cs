 using Microsoft.Extensions.Logging;
 using PreludeLib.Runtime;
 using PreludeLib.Runtime.Backend;
 using PreludeLib.Runtime.Backend.Dummy;

namespace PreludeLib.Tests.Payload.SpecialInjection;

public class DummySpecialInjectionPayload(ILogger logger) : SpecialInjectionPayloadBase(false, logger)
{
    protected override IRuntimeBackend CreateBackend()
        => new RuntimeDummyBackend();
}