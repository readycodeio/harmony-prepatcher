using Microsoft.Extensions.Logging;
using PreludeLib.Runtime.Backend;
using PreludeLib.Runtime.Backend.HarmonyDetour;

namespace PreludeLib.Payload.SpecialInjection;

public class HarmonySpecialInjectionPayload(ILogger logger) : SpecialInjectionPayloadBase(true, logger)
{
    protected override IRuntimeBackend CreateBackend()
        => new RuntimeHarmonyBackend(Logger);
}