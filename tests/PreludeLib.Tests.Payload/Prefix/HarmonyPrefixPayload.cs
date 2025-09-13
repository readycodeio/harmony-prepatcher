using Microsoft.Extensions.Logging;
using PreludeLib.Runtime;
using PreludeLib.Runtime.Backend.HarmonyDetour;

namespace PreludeLib.Payload.Prefix;

public class HarmonyPrefixPayload(ILogger logger) : PrefixPayloadBase(true, logger)
{
    protected override IPreludeBackend CreateBackend(string id)
        => new PreludeHarmonyBackend(id, Logger);
}