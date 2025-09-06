using System;
using System.Reflection;
using HarmonyLib;
using PreludeLib.Tests.Examples;
using PreludeLib.Tests.Patches;
using PreludeLib.Tests.Utils;
using Xunit;

namespace PreludeLib.Tests;

public class CalculatorHarmonyTests : IsolatedTestsBase
{
    [Fact]
    public void BaseBehavior_WithoutPatches_IsUnmodified()
        => RunInsideIsolatedContext(nameof(BaseBehavior_WithoutPatches_IsUnmodified_Inner));
    
    private void BaseBehavior_WithoutPatches_IsUnmodified_Inner()
    {
        var calc = new Calculator();

        Assert.Equal(5, calc.Add(2, 3));
        Assert.Equal(-1, calc.Subtract(2, 3));
        Assert.Equal(6, calc.Multiply(2, 3));
        Assert.Equal(2, calc.Divide(6, 3));
        Assert.Throws<DivideByZeroException>(() => calc.Divide(1, 0));
    }

    [Fact]
    public void Patched_Add_LogsPrefixAndPostfix_AndReturnsSameResult()
        => RunInsideIsolatedContext(nameof(Patched_Add_LogsPrefixAndPostfix_AndReturnsSameResult_Inner));

    private void Patched_Add_LogsPrefixAndPostfix_AndReturnsSameResult_Inner()
    {
        // Arrange
        var id = "harmony-test-" + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(id);
        TestLoggerProvider.Logger.Clear();

        // Apply all patches defined in CalculatorPatches
        harmony.CreateClassProcessor(typeof(CalculatorPatches)).Patch();

        try
        {
            var calc = new Calculator();

            // Act
            var result = calc.Add(2, 3);

            // Assert: result unchanged
            Assert.Equal(5, result);

            // Assert: prefix and postfix logs present
            var logs = TestLoggerProvider.Logger.Entries;
            Assert.Contains(logs, s => s.Contains("[PREFIX] About to add 2 + 3"));
            Assert.Contains(logs, s => s.Contains("[POSTFIX] Addition result: 2 + 3 = 5"));
        }
        finally
        {
            harmony.UnpatchAll(id);
        }
    }

    [Fact]
    public void Patched_Divide_ByZero_IsHandledByPrefix_ReturnsZero_AndFinalizerLogsSuccess()
        => RunInsideIsolatedContext(nameof(Patched_Divide_ByZero_IsHandledByPrefix_ReturnsZero_AndFinalizerLogsSuccess_Inner));
    
    private void Patched_Divide_ByZero_IsHandledByPrefix_ReturnsZero_AndFinalizerLogsSuccess_Inner()
    {
        // Arrange
        var id = "harmony-test-" + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(id);
        TestLoggerProvider.Logger.Clear();

        harmony.CreateClassProcessor(typeof(CalculatorPatches)).Patch();

        try
        {
            var calc = new Calculator();

            // Act: division by zero under patched behavior
            var result = calc.Divide(1, 0);

            // Assert: prefix prevented exception, returned 0
            Assert.Equal(0, result);

            // Assert logs: prefix, warning, finalizer success
            var logs = TestLoggerProvider.Logger.Entries;
            Assert.Contains(logs, s => s.Contains("[PREFIX] About to divide 1 / 0"));
            Assert.Contains(logs, s => s.Contains("Division by zero detected, returning 0"));
            Assert.Contains(logs, s => s.Contains("[FINALIZER] Divide(1, 0) completed successfully"));
        }
        finally
        {
            harmony.UnpatchAll(id);
        }
    }

    [Fact]
    public void FinalizerOnly_CatchesException_AndDoesNotSuppressIt()
        => RunInsideIsolatedContext(nameof(FinalizerOnly_CatchesException_AndDoesNotSuppressIt_Inner));
    
    private void FinalizerOnly_CatchesException_AndDoesNotSuppressIt_Inner()
    {
        // Arrange: patch ONLY the finalizer so original throws
        var id = "harmony-test-" + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(id);
        TestLoggerProvider.Logger.Clear();

        // Patch Divide with finalizer only
        var originalDivide = typeof(Calculator).GetMethod(nameof(Calculator.Divide),
            BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            types: new[] { typeof(int), typeof(int) },
            modifiers: null);

        Assert.NotNull(originalDivide);

        var finalizer = new HarmonyMethod(typeof(CalculatorPatches).GetMethod(nameof(CalculatorPatches.DivideFinalizer), BindingFlags.Public | BindingFlags.Static)!);
        harmony.Patch(originalDivide!, finalizer: finalizer);

        try
        {
            var calc = new Calculator();

            // Act + Assert: original exception still propagates (finalizer logs it)
            var ex = Assert.Throws<DivideByZeroException>(() => calc.Divide(1, 0));
            Assert.Contains("Cannot divide by zero", ex.Message);

            // Assert: finalizer logged the exception
            var logs = TestLoggerProvider.Logger.Entries;
            Assert.Contains(logs, s => s.Contains("[FINALIZER] Exception caught in Divide(1, 0):"));
        }
        finally
        {
            harmony.UnpatchAll(id);
        }
    }

    [Fact]
    public void Patched_Divide_NormalOperands_UnchangedResult_AndFinalizerLogsSuccess()
        => RunInsideIsolatedContext(nameof(Patched_Divide_NormalOperands_UnchangedResult_AndFinalizerLogsSuccess_Inner));
    
    private void Patched_Divide_NormalOperands_UnchangedResult_AndFinalizerLogsSuccess_Inner()
    {
        // Arrange
        var id = "harmony-test-" + Guid.NewGuid().ToString("N");
        var harmony = new Harmony(id);
        TestLoggerProvider.Logger.Clear();

        harmony.CreateClassProcessor(typeof(CalculatorPatches)).Patch();

        try
        {
            var calc = new Calculator();

            // Act
            var result = calc.Divide(8, 2);

            // Assert: math unchanged
            Assert.Equal(4, result);

            // Assert: prefix + finalizer success present
            var logs = TestLoggerProvider.Logger.Entries;
            Assert.Contains(logs, s => s.Contains("[PREFIX] About to divide 8 / 2"));
            Assert.Contains(logs, s => s.Contains("[FINALIZER] Divide(8, 2) completed successfully"));
        }
        finally
        {
            harmony.UnpatchAll(id);
        }
    }
}