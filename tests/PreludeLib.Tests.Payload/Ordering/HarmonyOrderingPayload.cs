using Microsoft.Extensions.Logging;
using PreludeLib.Runtime;
using PreludeLib.Runtime.Backend.HarmonyDetour;

namespace PreludeLib.Payload.Ordering;

public class HarmonyOrderingPayload(ILogger logger) : OrderingPayloadBase(true, logger)
{
    protected override IPreludeBackend CreateBackend(string id)
        => new PreludeHarmonyBackend(id, Logger);
}