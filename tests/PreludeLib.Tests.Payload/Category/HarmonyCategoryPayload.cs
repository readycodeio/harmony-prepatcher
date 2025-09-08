using Microsoft.Extensions.Logging;
using PreludeLib.Runtime;
using PreludeLib.Runtime.HarmonyBackend;

namespace PreludeLib.Payload.Category;

public class HarmonyCategoryPayload(ILogger logger) : CategoryPayloadBase(true, logger)
{
    protected override IPreludeBackend CreateBackend(string id)
        => new PreludeHarmonyBackend(id, Logger);
}