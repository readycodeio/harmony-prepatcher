using Microsoft.Extensions.Logging;
using PreludeLib.Tests.Examples;
using PreludeLib.Tests.Patches.Postfix;
using Xunit;

namespace PreludeLib.Payload.Postfix;

public abstract class PostfixPayloadBase(bool shouldPass, ILogger logger) : BackendPayloadBase(shouldPass, logger)
{
    public void PostfixCanReadAndModify__result()
    {
        var id = GenerateId(nameof(PostfixCanReadAndModify__result));
        var builder = CreateBuilder(id);
        var owner = GetOrCreatePrelude();

        builder.ScanAndPatch(typeof(PostfixModifyResultPatch));
        owner.Commit();

        try
        {
            var t = new PostfixTargets();
            // Original Double(7) => 14; postfix adds +5 => 19
            int result = t.Double(7);
            Assert.Equal(19, result);
        }
        finally
        {
            builder.UnpatchAll();
            owner.Commit();
        }
    }

    public void PostfixOnVoidMethodExecutes()
    {
        var id = GenerateId(nameof(PostfixOnVoidMethodExecutes));
        var builder = CreateBuilder(id);
        var owner = GetOrCreatePrelude();

        builder.ScanAndPatch(typeof(PostfixOnVoidPatch));
        owner.Commit();

        try
        {
            PostfixProbes.Reset();

            var t = new PostfixTargets();
            t.NoOp();

            Assert.True(PostfixProbes.VoidPostfixExecuted);
        }
        finally
        {
            builder.UnpatchAll();
            owner.Commit();
        }
    }

    public void PostfixReceivesStateFromPrefixVia__state()
    {
        var id = GenerateId(nameof(PostfixReceivesStateFromPrefixVia__state));
        var backend = CreateBuilder(id);
        var owner = GetOrCreatePrelude();
        
        backend.ScanAndPatch(typeof(PostfixStatePatch));
        owner.Commit();

        try
        {
            var t = new PostfixTargets();
            // Echo(3): original -> 3; prefix sets __state=30; postfix adds 30 => 33
            int result = t.Echo(3);
            Assert.Equal(33, result);
        }
        finally
        {
            backend.UnpatchAll();
            owner.Commit();
        }
    }

    public void PostfixSeesArgsAfterPrefixModifications()
    {
        var id = GenerateId(nameof(PostfixSeesArgsAfterPrefixModifications));
        var builder = CreateBuilder(id);
        var owner = GetOrCreatePrelude();
        
        builder.ScanAndPatch(typeof(PostfixSeesArgsAfterPrefixPatch));
        owner.Commit();

        try
        {
            PostfixProbes.Reset();

            var t = new PostfixTargets();
            // Prefix will mutate (a=5->6, b=10->12); Combine returns a+b = 18
            int result = t.Combine(5, 10);

            Assert.Equal(6, PostfixProbes.ObservedA);
            Assert.Equal(12, PostfixProbes.ObservedB);
            Assert.Equal(18, result);
        }
        finally
        {
            builder.UnpatchAll();
            owner.Commit();
        }
    }
}