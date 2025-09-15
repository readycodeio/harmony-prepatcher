using Microsoft.Extensions.Logging;
using PreludeLib.Runtime.Backend;
using PreludeLib.Runtime.Backend.HarmonyDetour;

namespace PreludeLib.Payload.Prefix;

public class HarmonyPrefixPayload(ILogger logger) : PrefixPayloadBase(true, logger)
{
    protected override IRuntimeBackend CreateBackend()
        => new RuntimeHarmonyBackend(Logger);
}