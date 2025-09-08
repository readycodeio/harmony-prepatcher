using Microsoft.Extensions.Logging;
using PreludeLib.Runtime;
using PreludeLib.Runtime.HarmonyBackend;

namespace PreludeLib.Payload.OverloadArg;

public class HarmonyOverloadArgPayload(ILogger logger) : OverloadArgPayloadBase(true, logger)
{
    protected override IPreludeBackend CreateBackend(string id)
        => new PreludeHarmonyBackend(id, Logger);
}