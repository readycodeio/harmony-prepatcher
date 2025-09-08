using Microsoft.Extensions.Logging;
using PreludeLib.Tests.Examples;
using PreludeLib.Tests.Patches.Finalizer;
using Xunit;
using Xunit.Sdk;

namespace PreludeLib.Payload.Finalizer;

public abstract class FinalizerPayloadBase(bool shouldPass, ILogger logger) : BackendPayloadBase(shouldPass, logger)
{
    public void FinalizerReceivesExceptionWhenOriginalThrows()
    {
        var id = GenerateId(nameof(FinalizerReceivesExceptionWhenOriginalThrows));
        var backend = CreateBackend(id);
        backend.CreateClassProcessor(typeof(FinalizerObservePatch)).Patch();

        try
        {
            FinalizerProbes.Reset();
            var t = new FinalizerTargets();

            var ex = Assert.Throws<InvalidOperationException>(() => t.MightThrow(-1));
            Assert.Contains("neg not allowed", ex.Message);

            Assert.True(FinalizerProbes.FinalizerRan);
            Assert.NotNull(FinalizerProbes.LastException);
            Assert.IsType<InvalidOperationException>(FinalizerProbes.LastException);
            Assert.Contains("neg not allowed", FinalizerProbes.LastException!.Message);
        }
        finally
        {
            FinalizerProbes.Reset();
            backend.UnpatchAll();
        }
    }

    public void FinalizerCanSuppressExceptionByReturningNull()
    {
        var id = GenerateId(nameof(FinalizerCanSuppressExceptionByReturningNull));
        var backend = CreateBackend(id);
        backend.CreateClassProcessor(typeof(FinalizerSuppressPatch)).Patch();

        try
        {
            FinalizerProbes.Reset();
            var t = new FinalizerTargets();

            // Original would throw; finalizer returns null (suppress) and sets __result = -99

            int result;
            try
            {
                result = t.MightThrow(-1);
            }
            catch (Exception ex)
            {
                throw new XunitException("Should not throw", ex);
            }

            Assert.True(FinalizerProbes.FinalizerRan);
            Assert.NotNull(FinalizerProbes.LastException);
            Assert.Equal(-99, result); // confirmed fallback result applied
        }
        finally
        {
            backend.UnpatchAll();
        }
    }

    public void FinalizerRunsOnSuccessfulExecutionAndSeesNullException()
    {
        var id = GenerateId(nameof(FinalizerRunsOnSuccessfulExecutionAndSeesNullException));
        var backend = CreateBackend(id);
        backend.CreateClassProcessor(typeof(FinalizerObservePatch)).Patch();

        try
        {
            FinalizerProbes.Reset();
            var t = new FinalizerTargets();

            int result = t.MightThrow(4); // no throw, result = 8
            Assert.Equal(8, result);

            Assert.True(FinalizerProbes.FinalizerRan);
            Assert.Null(FinalizerProbes.LastException);
        }
        finally
        {
            backend.UnpatchAll();
        }
    }
}