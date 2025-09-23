using Microsoft.Extensions.Logging;
using PreludeLib.Runtime.Backend;
using PreludeLib.Runtime.Backend.HarmonyDetour;

namespace PreludeLib.Payload.Foreach;

public class HarmonyForeachPayload(ILogger logger) : ForeachPayloadBase(true, logger)
{
    protected override IRuntimeBackend CreateBackend()
        => new RuntimeHarmonyBackend(Logger);
}