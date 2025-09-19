using Microsoft.Extensions.Logging;
using PreludeLib.Runtime.Backend;
using PreludeLib.Runtime.Backend.WeaverCallback;

namespace PreludeLib.Payload.Categories;

public class WeaverCategoryPayload(ILogger logger) : CategoryPayloadBase(true, logger)
{
    protected override IRuntimeBackend CreateBackend()
        => new RuntimeWeaverBackend(Logger);
}