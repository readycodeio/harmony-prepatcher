using Microsoft.Extensions.Logging;
using PreludeLib.Tests.Examples;
using PreludeLib.Tests.Patches.Properties;
using Xunit;

namespace PreludeLib.Payload.Properties;

public abstract class PropertiesPayloadBase(bool shouldPass, ILogger logger) : BackendPayloadBase(shouldPass, logger)
{
    public void PatchPropertySetterWithMethodTypeSetter()
    {
        var id = GenerateId(nameof(PatchPropertySetterWithMethodTypeSetter));
        var backend = CreateBackend(id);
        backend.CreateClassProcessor(typeof(PropertySetterPostfixPatch)).Patch();

        try
        {
            var t = new PropertyTargets();

            Assert.Equal(0, t.Counter);
            t.P = 5; // postfix should bump counter
            Assert.Equal(5, t.P);
            Assert.Equal(5, t.P_Raw());
            Assert.Equal(1, t.Counter);

            t.P = 9; // bump again
            Assert.Equal(9, t.P);
            Assert.Equal(2, t.Counter);
        }
        finally
        {
            backend.UnpatchAll();
        }
    }

    public void PrefixOnAutoPropertySetterCanModifyIncomingValue()
    {
        var id = GenerateId(nameof(PrefixOnAutoPropertySetterCanModifyIncomingValue));
        var harmony = CreateBackend(id);
        harmony.CreateClassProcessor(typeof(AutoSetterPrefixPatch)).Patch();

        try
        {
            var t = new PropertyTargets();

            t.Auto = 7;     // prefix converts to 17
            Assert.Equal(17, t.Auto);

            t.Auto = -3;    // prefix converts to 7
            Assert.Equal(7, t.Auto);
        }
        finally
        {
            harmony.UnpatchAll();
        }
    }
}