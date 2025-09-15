using Microsoft.Extensions.Logging;
using PreludeLib.Runtime;
using PreludeLib.Runtime.Backend;
using PreludeLib.Runtime.Backend.Dummy;

namespace PreludeLib.Payload.Properties;

public class DummyPropertiesPayload(ILogger logger) : PropertiesPayloadBase(false, logger)
{
    protected override IRuntimeBackend CreateBackend()
        => new RuntimeDummyBackend();
}