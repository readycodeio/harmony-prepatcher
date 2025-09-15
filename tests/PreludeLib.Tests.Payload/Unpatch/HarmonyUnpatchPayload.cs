using Microsoft.Extensions.Logging;
using PreludeLib.Runtime.Backend;
using PreludeLib.Runtime.Backend.HarmonyDetour;

namespace PreludeLib.Payload.Unpatch;

public class HarmonyUnpatchPayload(ILogger logger) : UnpatchPayloadBase(true, logger)
{
    protected override IRuntimeBackend CreateBackend()
        => new PreludeHarmonyBackend(Logger);
}