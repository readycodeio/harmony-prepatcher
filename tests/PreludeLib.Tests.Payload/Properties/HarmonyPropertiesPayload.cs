using Microsoft.Extensions.Logging;
using PreludeLib.Runtime;
using PreludeLib.Runtime.Backend;
using PreludeLib.Runtime.Backend.HarmonyDetour;

namespace PreludeLib.Payload.Properties;

public class HarmonyPropertiesPayload(ILogger logger) : PropertiesPayloadBase(true, logger)
{
    protected override IRuntimeBackend CreateBackend()
        => new PreludeHarmonyBackend(Logger);
}