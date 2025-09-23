using Microsoft.Extensions.Logging;
using PreludeLib.Tests.Examples;
using PreludeLib.Tests.Patches.Foreach;
using Xunit;

namespace PreludeLib.Payload.Foreach;

public abstract class ForeachPayloadBase(bool shouldPass, ILogger logger) : BackendPayloadBase(shouldPass, logger)
{
    public void PatchedMethodDoesNotThrowInvalidIlException()
    {
        var id = GenerateId(nameof(PatchedMethodDoesNotThrowInvalidIlException));
        var builder = CreateBuilder(id);
        var owner = GetOrCreatePrelude();

        builder.ScanAndPatch(typeof(ForeachSemaphorePatch));
        owner.Commit();

        try
        {
            var x = ForeachTargets.Example(out var _);
            Assert.Equal(shouldPass ? 5 : 7, x);
        }
        finally
        {
            builder.UnpatchAll();
            owner.Commit();
        }
    }
}