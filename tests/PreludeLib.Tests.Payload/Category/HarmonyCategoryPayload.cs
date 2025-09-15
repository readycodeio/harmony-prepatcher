using Microsoft.Extensions.Logging;
using PreludeLib.Runtime.Backend;
using PreludeLib.Runtime.Backend.HarmonyDetour;

namespace PreludeLib.Payload.Category;

public class HarmonyCategoryPayload(ILogger logger) : CategoryPayloadBase(true, logger)
{
    protected override IRuntimeBackend CreateBackend()
        => new PreludeHarmonyBackend(Logger);
}