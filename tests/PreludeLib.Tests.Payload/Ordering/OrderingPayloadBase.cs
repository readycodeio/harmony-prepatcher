using Microsoft.Extensions.Logging;
using PreludeLib.Tests.Examples;
using PreludeLib.Tests.Patches.Ordering;
using Xunit;

namespace PreludeLib.Payload.Ordering;

public abstract class OrderingPayloadBase(bool shouldPass, ILogger logger) : BackendPayloadBase(shouldPass, logger)
{
    public void IncrementalPrefixes_RespectPriorityAtEachStep()
    {
        var id = GenerateId(nameof(IncrementalPrefixes_RespectPriorityAtEachStep));
        var backend = CreateBackend(id);

        try
        {
            var t = new OrderStackTargets();

            // Step 1: Add A (VeryHigh)
            backend.CreateClassProcessor(typeof(PrefixVH_A)).Patch();
            OrderStackProbes.Reset();
            int r1 = t.Compute(1);          // A: 1*10+1 = 11
            Assert.Equal(new[] { "A" }, OrderStackProbes.Steps);
            Assert.Equal(11, r1);

            // Step 2: Add B (Low). Expect A then B.
            backend.CreateClassProcessor(typeof(PrefixLow_B)).Patch();
            OrderStackProbes.Reset();
            int r2 = t.Compute(1);          // A: 11 ; B: 112
            Assert.Equal(new[] { "A", "B" }, OrderStackProbes.Steps);
            Assert.Equal(112, r2);

            // Step 3: Add C (High). Expect A (VH), C (H), B (L).
            backend.CreateClassProcessor(typeof(PrefixHigh_C)).Patch();
            OrderStackProbes.Reset();
            int r3 = t.Compute(1);          // A: 11 ; C: 113 ; B: 1132
            Assert.Equal(new[] { "A", "C", "B" }, OrderStackProbes.Steps);
            Assert.Equal(1132, r3);
        }
        finally
        {
            backend.UnpatchAll();
        }
    }

    public void RegistrationOrderIrrelevant_PriorityDefinesOrder()
    {
        var id = GenerateId(nameof(RegistrationOrderIrrelevant_PriorityDefinesOrder));

        // Apply in order Low -> High -> VeryHigh
        var backend = CreateBackend(id);
        backend.CreateClassProcessor(typeof(PrefixLow_B)).Patch();
        backend.CreateClassProcessor(typeof(PrefixHigh_C)).Patch();
        backend.CreateClassProcessor(typeof(PrefixVH_A)).Patch();

        try
        {
            OrderStackProbes.Reset();
            var t = new OrderStackTargets();
            int result = t.Compute(1);      // Expect A -> C -> B
            Assert.Equal(new[] { "A", "C", "B" }, OrderStackProbes.Steps);
            Assert.Equal(1132, result);
        }
        finally
        {
            backend.UnpatchAll();
        }
    }

    public void CrossOwnerConstraints_EnforceZThenYThenX()
    {
        // Apply in shuffled order: X (after Y), then Z, then Y (after Z). Final must be Z -> Y -> X
        var id = GenerateId(nameof(CrossOwnerConstraints_EnforceZThenYThenX));
        var hX = CreateBackend($"{id}-{StackOwners.OwnerX}");
        var hZ = CreateBackend($"{id}-{StackOwners.OwnerZ}");
        var hY = CreateBackend($"{id}-{StackOwners.OwnerY}");

        hX.CreateClassProcessor(typeof(OwnerX_Prefix)).Patch();
        hZ.CreateClassProcessor(typeof(OwnerZ_Prefix)).Patch();
        hY.CreateClassProcessor(typeof(OwnerY_Prefix)).Patch();

        try
        {
            OrderStackProbes.Reset();
            var t = new OrderStackTargets();
            int result = t.Compute(1);
            // Z: 1*10+3 = 13; Y: 13*10+2 = 132; X: 132*10+1 = 1321
            Assert.Equal(new[] { "Z", "Y", "X" }, OrderStackProbes.Steps);
            Assert.Equal(1321, result);
        }
        finally
        {
            hX.UnpatchAll();
            hY.UnpatchAll();
            hZ.UnpatchAll();
        }
    }

    public void PostfixOrdering_RespectsBeforeAfterConstraints()
    {
        var id = GenerateId(nameof(PostfixOrdering_RespectsBeforeAfterConstraints));
        var hp = CreateBackend($"{id}-{PostfixOwners.OwnerP}");
        var hq = CreateBackend($"{id}-{PostfixOwners.OwnerQ}");

        // Apply in reverse to ensure registration order doesn't help
        hq.CreateClassProcessor(typeof(PostfixQ)).Patch();
        hp.CreateClassProcessor(typeof(PostfixP)).Patch();

        try
        {
            OrderStackProbes.Reset();
            var t = new OrderStackTargets();

            // Start with 2: postfix P runs first (due to [HarmonyBefore(Q)]), then Q
            // P: 2 -> 24 ; Q: 24 -> 245
            int result = t.Compute(2);
            Assert.Equal(new[] { "P", "Q" }, OrderStackProbes.Steps);
            Assert.Equal(245, result);
        }
        finally
        {
            hp.UnpatchAll();
            hq.UnpatchAll();
        }
    }

    public void FinalizerRunsAfterPostfixes()
    {
        var id = GenerateId(nameof(FinalizerRunsAfterPostfixes));
        var harmony = CreateBackend(id);

        harmony.CreateClassProcessor(typeof(PostfixP)).Patch();
        harmony.CreateClassProcessor(typeof(FinalizerTag)).Patch();

        try
        {
            OrderStackProbes.Reset();
            var t = new OrderStackTargets();

            int result = t.Compute(3);   // P: 3 -> 34 ; F after
            Assert.Equal(new[] { "P", "F" }, OrderStackProbes.Steps);
            Assert.Equal(34, result);
        }
        finally
        {
            harmony.UnpatchAll();
        }
    }

    public void IncrementalAdd_WithCrossOwnerConstraints_StableFinalOrder()
    {
        var id = GenerateId(nameof(IncrementalAdd_WithCrossOwnerConstraints_StableFinalOrder));
        var hZ = CreateBackend($"{id}-{StackOwners.OwnerZ}");
        var hX = CreateBackend($"{id}-{StackOwners.OwnerX}");
        var hY = CreateBackend($"{id}-{StackOwners.OwnerY}");

        try
        {
            // Step 1: Add X only (claims After Y). With only X, it just runs alone.
            hX.CreateClassProcessor(typeof(OwnerX_Prefix)).Patch();
            OrderStackProbes.Reset();
            var t = new OrderStackTargets();
            int r1 = t.Compute(1); // X alone: 1 -> 11
            Assert.Equal(new[] { "X" }, OrderStackProbes.Steps);
            Assert.Equal(11, r1);

            // Step 2: Add Z. Constraints now imply Z should run before X (since X After Y, Y After Z; w/o Y, X can still run but Z should come first).
            hZ.CreateClassProcessor(typeof(OwnerZ_Prefix)).Patch();
            OrderStackProbes.Reset();
            int r2 = t.Compute(1); // Z then X: 1->13->131
            Assert.Equal(new[] { "Z", "X" }, OrderStackProbes.Steps);
            Assert.Equal(131, r2);

            // Step 3: Add Y (which must run after Z, and X must run after Y). Final order: Z -> Y -> X.
            hY.CreateClassProcessor(typeof(OwnerY_Prefix)).Patch();
            OrderStackProbes.Reset();
            int r3 = t.Compute(1); // Z:13 ; Y:132 ; X:1321
            Assert.Equal(new[] { "Z", "Y", "X" }, OrderStackProbes.Steps);
            Assert.Equal(1321, r3);
        }
        finally
        {
            // Cleanup
            hX.UnpatchAll();
            hY.UnpatchAll();
            hZ.UnpatchAll();
        }
    }
}