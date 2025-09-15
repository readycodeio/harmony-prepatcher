using Microsoft.Extensions.Logging;
using PreludeLib.Runtime.Backend;
using PreludeLib.Runtime.Backend.HarmonyDetour;

namespace PreludeLib.Payload.Ordering;

public class HarmonyOrderingPayload(ILogger logger) : OrderingPayloadBase(true, logger)
{
    protected override IRuntimeBackend CreateBackend()
        => new PreludeHarmonyBackend(Logger);
}