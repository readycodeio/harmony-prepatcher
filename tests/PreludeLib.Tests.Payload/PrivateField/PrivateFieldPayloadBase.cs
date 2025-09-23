using Microsoft.Extensions.Logging;
using PreludeLib.Tests.Examples;
using PreludeLib.Tests.Patches.PrivateField;
using Xunit;

namespace PreludeLib.Tests.Payload.PrivateField;

public abstract class PrivateFieldPayloadBase(bool shouldPass, ILogger logger) : BackendPayloadBase(shouldPass, logger)
{
    public void PrefixCanReadPrivateFieldViaTripleUnderscore()
    {
        var id = GenerateId(nameof(PrefixCanReadPrivateFieldViaTripleUnderscore));
        var builder = CreateBuilder(id);
        var owner = GetOrCreatePrelude();
        
        builder.ScanAndPatch(typeof(PrivateFieldReadPrefixPatch));
        owner.Commit();

        try
        {
            PrivateFieldProbes.Reset();
            var t = new PrivateFieldTargets();

            // Default private 'secret' is 5; Bump(2) => 7
            int result = t.Bump(2);

            Assert.Equal(5, PrivateFieldProbes.PrefixSeenSecret);
            Assert.Equal(7, result);
            Assert.Equal(5, t.GetSecret()); // field itself unchanged by this test
        }
        finally
        {
            builder.UnpatchAll();
            owner.Commit();
        }
    }
    
    public void PrefixCanModifyPrivateFieldViaRefTripleUnderscore()
    {
        var id = GenerateId(nameof(PrefixCanModifyPrivateFieldViaRefTripleUnderscore));
        var builder = CreateBuilder(id);
        var owner = GetOrCreatePrelude();
        
        builder.ScanAndPatch(typeof(PrivateFieldModifyAndObservePatch));
        owner.Commit();

        try
        {
            PrivateFieldProbes.Reset();
            var t = new PrivateFieldTargets();

            // Prefix adds +10 to secret (5->15); Bump(3) => 15 + 3 = 18
            int result = t.Bump(3);

            Assert.Equal(18, result);
            Assert.Equal(15, t.GetSecret());
            Assert.Equal(15, PrivateFieldProbes.PrefixSeenSecret);
        }
        finally
        {
            builder.UnpatchAll();
            owner.Commit();
        }
    }

    public void PostfixCanObservePrivateFieldChangesFromPrefix()
    {
        var id = GenerateId(nameof(PostfixCanObservePrivateFieldChangesFromPrefix));
        var builder = CreateBuilder(id);
        var owner = GetOrCreatePrelude();
        
        builder.ScanAndPatch(typeof(PrivateFieldModifyAndObservePatch));
        owner.Commit();

        try
        {
            PrivateFieldProbes.Reset();
            var t = new PrivateFieldTargets();

            // Same mutation path: 5 -> 15 in prefix
            int result = t.Bump(1); // 15 + 1 = 16

            Assert.Equal(16, result);
            Assert.Equal(15, PrivateFieldProbes.PostfixSeenSecret); // postfix should see 15
            Assert.Equal(15, t.GetSecret());
        }
        finally
        {
            builder.UnpatchAll();
            owner.Commit();
        }
    }
}