using Microsoft.Extensions.Logging;
using PreludeLib.Runtime;
using PreludeLib.Runtime.HarmonyBackend;

namespace PreludeLib.Payload.Properties;

public class HarmonyPropertiesPayload(ILogger logger) : PropertiesPayloadBase(true, logger)
{
    protected override IPreludeBackend CreateBackend(string id)
        => new PreludeHarmonyBackend(id, Logger);
}