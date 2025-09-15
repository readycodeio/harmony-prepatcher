using Microsoft.Extensions.Logging;
using PreludeLib.Runtime.Backend;
using PreludeLib.Runtime.Backend.HarmonyDetour;

namespace PreludeLib.Payload.OverloadArg;

public class HarmonyOverloadArgPayload(ILogger logger) : OverloadArgPayloadBase(true, logger)
{
    protected override IRuntimeBackend CreateBackend()
        => new RuntimeHarmonyBackend(Logger);
}