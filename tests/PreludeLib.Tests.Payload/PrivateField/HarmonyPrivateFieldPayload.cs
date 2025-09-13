using Microsoft.Extensions.Logging;
using PreludeLib.Runtime;
using PreludeLib.Runtime.Backend.HarmonyDetour;

namespace PreludeLib.Payload.PrivateField;

public class HarmonyPrivateFieldPayload(ILogger logger) : PrivateFieldPayloadBase(true, logger)
{
    protected override IPreludeBackend CreateBackend(string id)
        => new PreludeHarmonyBackend(id, Logger);
}