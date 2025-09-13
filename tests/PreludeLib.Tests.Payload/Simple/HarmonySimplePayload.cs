using Microsoft.Extensions.Logging;
using PreludeLib.Runtime;
using PreludeLib.Runtime.Backend.HarmonyDetour;

namespace PreludeLib.Payload.Simple;

public class HarmonySimplePayload(ILogger logger) : SimplePayloadBase(true, logger)
{
    protected override IPreludeBackend CreateBackend(string id)
        => new PreludeHarmonyBackend(id, Logger);
}