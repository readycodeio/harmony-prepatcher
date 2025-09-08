using System.Reflection;
using Microsoft.Extensions.Logging;
using PreludeLib.Runtime;
using PreludeLib.Tests.Examples;
using PreludeLib.Tests.Patches.Unpatch;
using Xunit;

namespace PreludeLib.Payload.Unpatch;

public abstract class UnpatchPayloadBase(bool shouldPass, ILogger logger) : BackendPayloadBase(shouldPass, logger)
{
    public void UnpatchSpecificPrefix_MiddleRemoval_KeepsOrderStable()
    {
        var id = GenerateId(nameof(UnpatchSpecificPrefix_MiddleRemoval_KeepsOrderStable));
        Logger.LogDebug("ID: {id}", id);
        
        var backend = CreateBackend(id);
        try
        {
            UnpatchProbes.Reset();
            var t = new UnpatchTargets();

            int r0 = t.Compute(1);
            Assert.Equal([], UnpatchProbes.Steps);
            Assert.Equal(1, r0);

            backend.CreateClassProcessor(typeof(UnpatchPrefixA)).Patch();
            backend.CreateClassProcessor(typeof(UnpatchPrefixB)).Patch();
            backend.CreateClassProcessor(typeof(UnpatchPrefixC)).Patch();

            // Initial run: expect A -> B -> C
            int r1 = t.Compute(1);
            Assert.Equal(new[] { "A", "B", "C" }, UnpatchProbes.Steps);
            Assert.Equal(1123, r1);

            // Now unpatch ONLY B (remove the specific patch method)
            UnpatchProbes.Steps.Clear();

            var original = typeof(UnpatchTargets).GetMethod(nameof(UnpatchTargets.Compute))!;
            var bPatchMethod = typeof(UnpatchPrefixB).GetMethod("Prefix", BindingFlags.Public | BindingFlags.Static)!;
            backend.Unpatch(original, bPatchMethod); // <-- corrected overload

            // Run again: expect A -> C with same relative order
            int r2 = t.Compute(1);
            Assert.Equal(new[] { "A", "C" }, UnpatchProbes.Steps);
            Assert.Equal(113, r2);

            UnpatchProbes.Steps.Clear();

            var aPatchMethod = typeof(UnpatchPrefixA).GetMethod("Prefix", BindingFlags.Public | BindingFlags.Static)!;
            backend.Unpatch(original, aPatchMethod); // <-- corrected overload

            // Run again: expect C with same relative order
            t.Compute(1);
            Assert.Equal(new[] { "C" }, UnpatchProbes.Steps);

            UnpatchProbes.Steps.Clear();

            backend.Patch(original, prefix: new PreludeMethod(bPatchMethod)); // <-- corrected overload

            // Run again: expect C with same relative order
            t.Compute(1);
            
            Assert.Equal(new[] { "B", "C" }, UnpatchProbes.Steps);
        }
        finally
        {
            backend.UnpatchAll();
        }
    }
    
    public void UnpatchAll_ByOwnerId_RemovesOnlyThatOwnersPatches()
    {
        // Patch with two distinct owners; apply A first then B.
        // Then unpatch A (not reverse order), B remains.
        var id = GenerateId(nameof(UnpatchSpecificPrefix_MiddleRemoval_KeepsOrderStable));
        var backendA = CreateBackend($"{id}-{UnpatchOwners.OwnerA}");
        var backendB = CreateBackend($"{id}-{UnpatchOwners.OwnerB}");

        backendA.CreateClassProcessor(typeof(OwnerA_PrefixPatch)).Patch();
        backendB.CreateClassProcessor(typeof(OwnerB_PrefixPatch)).Patch();

        try
        {
            UnpatchProbes.Reset();
            var t = new UnpatchTargets();

            // Both owners active: A then B (by priority)
            int r1 = t.Compute(1);
            Assert.Equal(new[] { "A", "B" }, UnpatchProbes.Steps);
            Assert.Equal(112, r1);

            // Unpatch ONLY OwnerA (order-independent)
            UnpatchProbes.Steps.Clear();
            backendA.UnpatchAll();

            int r2 = t.Compute(1); // only B now
            Assert.Equal(new[] { "B" }, UnpatchProbes.Steps);
            Assert.Equal(12, r2);
        }
        finally
        {
            backendA.UnpatchAll();
            backendB.UnpatchAll();
        }
    }

    public void UnpatchSpecificPostfix_LeavesPrefixAndFinalizerActive()
    {
        var id = GenerateId(nameof(UnpatchSpecificPostfix_LeavesPrefixAndFinalizerActive));
        var backend = CreateBackend(id);
        backend.CreateClassProcessor(typeof(MixedPrefixPatch)).Patch();
        backend.CreateClassProcessor(typeof(MixedPostfixPatch)).Patch();
        backend.CreateClassProcessor(typeof(MixedFinalizerPatch)).Patch();

        try
        {
            UnpatchProbes.Reset();
            var t = new UnpatchTargets();

            // Initial run: Prefix -> Postfix -> Finalizer
            int r1 = t.Compute(2); // Prefix: 2->21 ; Postfix: +100 => 121
            Assert.Equal(new[] { "Pre", "Post", "Fin" }, UnpatchProbes.Steps);
            Assert.Equal(121, r1);

            // Remove ONLY the postfix by its MethodInfo
            UnpatchProbes.Steps.Clear();
            var original = typeof(UnpatchTargets).GetMethod(nameof(UnpatchTargets.Compute))!;
            backend.Unpatch(original, MixedPostfixPatch.MethodInfo()); // <-- corrected overload

            int r2 = t.Compute(2); // Prefix only: 2->21 ; Finalizer runs; no +100
            Assert.Equal(new[] { "Pre", "Fin" }, UnpatchProbes.Steps);
            Assert.Equal(21, r2);
        }
        finally
        {
            backend.UnpatchAll();
        }
    }
}