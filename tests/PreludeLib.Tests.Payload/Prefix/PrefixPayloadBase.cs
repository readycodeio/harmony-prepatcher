using Microsoft.Extensions.Logging;
using PreludeLib.Tests.Examples;
using PreludeLib.Tests.Patches.Prefix;
using Xunit;

namespace PreludeLib.Payload.Prefix;

public abstract class PrefixPayloadBase(bool shouldPass, ILogger logger) : BackendPayloadBase(shouldPass, logger)
{
    public void PrefixReturningFalseSkipsOriginalAndSetsResult()
    {
        var id = GenerateId(nameof(PrefixReturningFalseSkipsOriginalAndSetsResult));
        var builder = CreateBuilder(id);
        var owner = GetOrCreatePrelude();
        
        builder.ScanAndPatch(typeof(PrefixSkipSetResultPatch));
        owner.Commit();

        try
        {
            var t = new PrefixTargets();
            var result = t.Sum(2, 3); // normally 5
            Assert.Equal(-1, result);
        }
        finally
        {
            builder.UnpatchAll();
            owner.Commit();
        }
    }

    public void PrefixCanModifyByRefArguments()
    {
        var id = GenerateId(nameof(PrefixCanModifyByRefArguments));
        var builder = CreateBuilder(id);
        var owner = GetOrCreatePrelude();
        
        builder.ScanAndPatch(typeof(PrefixModifyByRefPatch));
        owner.Commit();

        try
        {
            var t = new PrefixTargets();
            int x = 10;
            int result = t.MultiplyRef(ref x, 3); // prefix adds 2 to x before original

            Assert.Equal(12, x);
            Assert.Equal(36, result);
        }
        finally
        {
            builder.UnpatchAll();
            owner.Commit();
        }
    }

    public void PrefixCanSetOutParameterValues()
    {
        var id = GenerateId(nameof(PrefixCanSetOutParameterValues));
        var builder = CreateBuilder(id);
        var owner = GetOrCreatePrelude();
        
        builder.ScanAndPatch(typeof(PrefixSetOutPatch));
        owner.Commit();

        try
        {
            var t = new PrefixTargets();
            t.MakePair(2, out int a, out int b); // prefix sets a=20, b=200 and skips original

            Assert.Equal(20, a);
            Assert.Equal(200, b);
        }
        finally
        {
            builder.UnpatchAll();
            owner.Commit();
        }
    }
    
    public void PrefixCanUseArgumentIndexAliases__0__1()
    {
        var id = GenerateId(nameof(PrefixCanUseArgumentIndexAliases__0__1));
        var builder = CreateBuilder(id);
        var owner = GetOrCreatePrelude();
        
        builder.ScanAndPatch(typeof(PrefixAliasesPatch));
        owner.Commit();

        try
        {
            var t = new PrefixTargets();
            // __0 is mutated by +7; 5 + 7 = 12, 12 + 1 = 13
            int result = t.Sum(5, 1);

            Assert.Equal(13, result);
        }
        finally
        {
            builder.UnpatchAll();
            owner.Commit();
        }
    }
}