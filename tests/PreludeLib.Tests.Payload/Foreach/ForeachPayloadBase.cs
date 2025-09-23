using Microsoft.Extensions.Logging;
using PreludeLib.Tests.Payload;
using PreludeLib.Tests.Examples;
using PreludeLib.Tests.Patches.Foreach;
using Xunit;

namespace PreludeLib.Tests.Payload.Foreach;

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
            Assert.Equal(5, x);
        }
        finally
        {
            builder.UnpatchAll();
            owner.Commit();
        }
    }
    
    public void WorksWithNestedForeachLoops()
    {
        var id = GenerateId(nameof(WorksWithNestedForeachLoops));
        var builder = CreateBuilder(id);
        var owner = GetOrCreatePrelude();

        builder.ScanAndPatch(typeof(ForeachNestedPatch));
        owner.Commit();

        try
        {
            Assert.Equal(555, ForeachTargets.NestedExample(6, 6, 6));
            Assert.Throws<ArgumentException>(() => ForeachTargets.NestedExample(7, 7, 7));
        }
        finally
        {
            builder.UnpatchAll();
            owner.Commit();
        }
    }
}