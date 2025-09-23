using Microsoft.Extensions.Logging;
using PreludeLib.Tests.Examples;
using PreludeLib.Tests.Patches.Targeting;
using Xunit;

namespace PreludeLib.Tests.Payload.Targeting;

public abstract class TargetingPayloadBase(bool shouldPass, ILogger logger) : BackendPayloadBase(shouldPass, logger)
{
    public void PatchByMethodNameAndSignatureTargetsCorrectOverload()
    {
        var id = GenerateId(nameof(PatchByMethodNameAndSignatureTargetsCorrectOverload));
        var builder = CreateBuilder(id);
        var owner = GetOrCreatePrelude();
        
        builder.ScanAndPatch(typeof(Overload2PostfixPatch));
        owner.Commit();

        try
        {
            TargetingProbes.Reset();
            var t = new TargetingExamples();

            // Call 2-arg overload: original 1 + 5 + 10 = 16; postfix adds +1000 => 1016
            int r2 = t.Over(5, 10);
            Assert.Equal(1016, r2);
            Assert.True(TargetingProbes.Over2PostfixHit);

            // Call 1-arg overload: should be unaffected => 1 + 7 = 8
            int r1 = t.Over(7);
            Assert.Equal(8, r1);

            // Call ref overload: should be unaffected; x is mutated by +_base (1)
            int x = 20;
            int rr = t.Over(ref x);
            Assert.Equal(21, x);
            Assert.Equal(21, rr);
        }
        finally
        {
            builder.UnpatchAll();
            owner.Commit();
        }
    }

    public void PatchPropertyGetterWithMethodTypeGetter()
    {
        var id = GenerateId(nameof(PatchPropertyGetterWithMethodTypeGetter));
        var builder = CreateBuilder(id);
        var owner = GetOrCreatePrelude();
        
        builder.ScanAndPatch(typeof(PropertyGetterPostfixPatch));
        owner.Commit();

        try
        {
            TargetingProbes.Reset();
            var t = new TargetingExamples();

            t.Value = 5;              // setter unaffected
            int v = t.Value;          // original = 100 + 5 = 105; postfix adds +10 => 115
            Assert.Equal(115, v);
            Assert.True(TargetingProbes.GetterPostfixHit);

            t.Value = 7;
            int v2 = t.Value;         // 100 + 7 + 10 => 117
            Assert.Equal(117, v2);
        }
        finally
        {
            builder.UnpatchAll();
            owner.Commit();
        }
    }

    public void PatchConstructorWithMethodTypeConstructor()
    {
        var id = GenerateId(nameof(PatchConstructorWithMethodTypeConstructor));
        var builder = CreateBuilder(id);
        var owner = GetOrCreatePrelude();
        
        builder.ScanAndPatch(typeof(CtorIntPostfixPatch));
        owner.Commit();

        try
        {
            TargetingProbes.Reset();

            // Construct with int overload: patch should run
            var ti = new TargetingExamples(42);
            Assert.True(TargetingProbes.CtorIntPostfixHit);
            Assert.Equal(42, TargetingProbes.CtorSeenBaseVal);
            Assert.Equal("int", ti.CtorTag); // ensure the correct ctor executed

            // Construct with default ctor: patch should NOT run (still true from earlier call, so reset first)
            TargetingProbes.CtorIntPostfixHit = false;
            var td = new TargetingExamples();
            Assert.False(TargetingProbes.CtorIntPostfixHit);
            Assert.Equal("default", td.CtorTag);
        }
        finally
        {
            builder.UnpatchAll();
            owner.Commit();
        }
    }
}