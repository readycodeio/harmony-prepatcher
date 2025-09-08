using Microsoft.Extensions.Logging;
using PreludeLib.Tests.Examples;
using PreludeLib.Tests.Patches.SpecialInjection;
using Xunit;

namespace PreludeLib.Payload.SpecialInjection;

public abstract class SpecialInjectionPayloadBase(bool shouldPass, ILogger logger) : BackendPayloadBase(shouldPass, logger)
{
    public void Injected__instanceProvidesOriginalInstance()
    {
        var id = GenerateId(nameof(Injected__instanceProvidesOriginalInstance));
        var backend = CreateBackend(id);
        backend.CreateClassProcessor(typeof(InstanceInjectionPrefixPatch)).Patch();

        try
        {
            SpecialInjectionProbes.Reset();
            var t = new SpecialInjectionTargets(offset: 1);

            // Prefix sets offset to 10 via __instance; result should be 5 + 10 = 15
            int result = t.SumWithOffset(5);

            Assert.Equal(10, t.GetOffset());
            Assert.Equal(15, result);
            Assert.Same(t, SpecialInjectionProbes.LastInstance);
        }
        finally
        {
            backend.UnpatchAll();
        }
    }

    public void Injected__argsArrayCanMutateArgumentsInPlace()
    {
        var id = GenerateId(nameof(Injected__argsArrayCanMutateArgumentsInPlace));
        var backend = CreateBackend(id);
        backend.CreateClassProcessor(typeof(ArgsArrayInjectionPrefixPatch)).Patch();

        try
        {
            SpecialInjectionProbes.Reset();
            var t = new SpecialInjectionTargets();

            // Prefix mutates (3 -> 4) and (4 -> 6); original Add sees 4 + 6 = 10
            int result = t.Add(3, 4);

            Assert.NotNull(SpecialInjectionProbes.LastArgsSnapshot);
            Assert.Equal(new[] { 4, 6 }, SpecialInjectionProbes.LastArgsSnapshot!);
            Assert.Equal(10, result);
        }
        finally
        {
            backend.UnpatchAll();
        }
    }

    public void Injected__originalMethodProvidesMethodBase()
    {
        var id = GenerateId(nameof(Injected__originalMethodProvidesMethodBase));
        var backend = CreateBackend(id);
        backend.CreateClassProcessor(typeof(OriginalMethodInjectionPostfixPatch)).Patch();

        try
        {
            SpecialInjectionProbes.Reset();
            var t = new SpecialInjectionTargets();

            int result = t.Combine(1, 2); // 1*100 + 2 = 102
            Assert.Equal(102, result);

            Assert.NotNull(SpecialInjectionProbes.LastOriginal);
            Assert.Equal("Combine", SpecialInjectionProbes.LastOriginal!.Name);
            Assert.Equal(typeof(SpecialInjectionTargets), SpecialInjectionProbes.LastOriginal!.DeclaringType);
        }
        finally
        {
            backend.UnpatchAll();
        }
    }

    public void HarmonyArgumentAttributeBindsByIndexAndName()
    {
        var id = GenerateId(nameof(HarmonyArgumentAttributeBindsByIndexAndName));
        var backend = CreateBackend(id);
        // Apply BOTH: prefix (argument binding) + postfix (original method capture)
        backend.CreateClassProcessor(typeof(HarmonyArgumentBindingPrefixPatch)).Patch();
        backend.CreateClassProcessor(typeof(OriginalMethodInjectionPostfixPatch)).Patch();

        try
        {
            SpecialInjectionProbes.Reset();
            var t = new SpecialInjectionTargets();

            // Prefix: left+=2, right+=3 -> (5+2, 1+3) -> Combine => 7*100 + 4 = 704
            int result = t.Combine(5, 1);
            Assert.Equal(704, result);

            // Also validate __originalMethod still reports the correct method
            Assert.NotNull(SpecialInjectionProbes.LastOriginal);
            Assert.Equal("Combine", SpecialInjectionProbes.LastOriginal!.Name);
            Assert.Equal(typeof(SpecialInjectionTargets), SpecialInjectionProbes.LastOriginal!.DeclaringType);
        }
        finally
        {
            backend.UnpatchAll();
        }
    }
}
