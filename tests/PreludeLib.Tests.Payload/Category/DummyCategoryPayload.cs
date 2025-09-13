using Microsoft.Extensions.Logging;
using PreludeLib.Runtime;
using PreludeLib.Runtime.Backend.Dummy;

namespace PreludeLib.Payload.Category;

public class DummyCategoryPayload(ILogger logger) : CategoryPayloadBase(false, logger)
{
    protected override IPreludeBackend CreateBackend(string id)
        => new PreludeDummyBackend(id);
}