using Microsoft.Extensions.Logging;
using PreludeLib.Runtime;
using PreludeLib.Runtime.DummyBackend;

namespace PreludeLib.Payload.Targeting;

public class DummyTargetingPayload(ILogger logger) : TargetingPayloadBase(false, logger)
{
    protected override IPreludeBackend CreateBackend(string id)
        => new PreludeDummyBackend(id);    
}