using Microsoft.Extensions.Logging;
using PreludeLib.Runtime.Backend;
using PreludeLib.Runtime.Backend.HarmonyDetour;

namespace PreludeLib.Payload.Postfix;

public class HarmonyPostfixPayload(ILogger logger) : PostfixPayloadBase(true, logger)
{
    protected override IRuntimeBackend CreateBackend()
        => new RuntimeHarmonyBackend(Logger);
}