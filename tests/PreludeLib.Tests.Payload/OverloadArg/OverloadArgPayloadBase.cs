using Microsoft.Extensions.Logging;
using PreludeLib.Tests.Examples;
using PreludeLib.Tests.Patches.OverloadArg;
using Xunit;

namespace PreludeLib.Tests.Payload.OverloadArg;

public abstract class OverloadArgPayloadBase(bool shouldPass, ILogger logger) : BackendPayloadBase(shouldPass, logger)
{
    public void PatchOverloadWithByRefArgumentUsingArgumentTypeRef()
    {
        var id = GenerateId(nameof(PatchOverloadWithByRefArgumentUsingArgumentTypeRef));
        var builder = CreateBuilder(id);
        var owner = GetOrCreatePrelude();
        
        builder.ScanAndPatch(typeof(IncRefOverloadPostfixPatch));
        owner.Commit();

        try
        {
            var t = new OverloadArgTargets();

            // Control: non-ref overload must be unaffected
            Assert.Equal(6, t.Inc(5));

            // Patched: ref overload should be targeted and modified by postfix
            int x = 10;
            int result = t.Inc(ref x);   // original: x becomes 11, returns 11; postfix adds +1000 => 1011

            Assert.Equal(11, x);         // ref arg still mutated by original
            Assert.Equal(1011, result);  // postfix modification visible
        }
        finally
        {
            builder.UnpatchAll();
            owner.Commit();
        }
    }
    
    public void PatchOverloadWithOutArgumentUsingArgumentTypeOut()
    {
        var id = GenerateId(nameof(PatchOverloadWithOutArgumentUsingArgumentTypeOut));
        var backend = CreateBuilder(id);
        var owner = GetOrCreatePrelude();

        backend.ScanAndPatch(typeof(TryMakeOutOverloadPrefixPatch));
        owner.Commit();

        try
        {
            var t = new OverloadArgTargets();

            // Patched overload: TryMake(out int) is intercepted and skipped; value should be 999.
            bool ok1 = t.TryMake(out int val1);
            Assert.True(ok1);
            Assert.Equal(999, val1);

            // Control overload: TryMake(int seed, out int) must be unaffected.
            bool ok2 = t.TryMake(7, out int val2);
            Assert.True(ok2);
            Assert.Equal(14, val2); // original behavior: seed * 2
        }
        finally
        {
            backend.UnpatchAll();
            owner.Commit();
        }
    }
}