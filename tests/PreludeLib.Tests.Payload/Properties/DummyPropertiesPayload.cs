using Microsoft.Extensions.Logging;
using PreludeLib.Runtime;
using PreludeLib.Runtime.DummyBackend;

namespace PreludeLib.Payload.Properties;

public class DummyPropertiesPayload(ILogger logger) : PropertiesPayloadBase(false, logger)
{
    protected override IPreludeBackend CreateBackend(string id)
        => new PreludeDummyBackend(id);
}