using System.Reflection;
using Microsoft.Extensions.Logging;
using PreludeLib.Runtime;
using PreludeLib.Tests.Examples;
using PreludeLib.Tests.Patches.Simple;
using Xunit;
using Xunit.Sdk;

namespace PreludeLib.Payload.Simple;

public abstract class SimplePayloadBase(bool shouldPass, ILogger logger) : BackendPayloadBase(shouldPass, logger)
{
    public void BaseBehavior_WithoutPatches_IsUnmodified()
    {
        var calc = new Calculator();

        TestLoggerProvider.Logger.Clear();
        
        Assert.Equal(5, calc.Add(2, 3));
        Assert.Equal(-1, calc.Subtract(2, 3));
        Assert.Equal(6, calc.Multiply(2, 3));
        Assert.Equal(2, calc.Divide(6, 3));
        Assert.Throws<DivideByZeroException>(() => calc.Divide(1, 0));
        
        Assert.Empty(TestLoggerProvider.Logger.Entries);
    }

    public void Patched_Add_LogsPrefixAndPostfix_AndReturnsSameResult()
    {
        // Arrange
        var id = GenerateId(nameof(Patched_Add_LogsPrefixAndPostfix_AndReturnsSameResult));
        var backend = CreateBackend(id);
        TestLoggerProvider.Logger.Clear();

        // Apply all patches defined in CalculatorPatches
        backend.CreateClassProcessor(typeof(CalculatorPatches)).Patch();

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
            backend.UnpatchAll();
        }
    }
    
    public void Patched_Divide_ByZero_IsHandledByPrefix_ReturnsZero_AndFinalizerLogsSuccess()
    {
        // Arrange
        var id = GenerateId(nameof(Patched_Divide_ByZero_IsHandledByPrefix_ReturnsZero_AndFinalizerLogsSuccess));
        var backend = CreateBackend(id);
        TestLoggerProvider.Logger.Clear();

        backend.CreateClassProcessor(typeof(CalculatorPatches)).Patch();

        try
        {
            var calc = new Calculator();

            int result;
            
            // Act: division by zero under patched behavior
            try
            {
                result = calc.Divide(1, 0);
            }
            catch (DivideByZeroException ex)
            {
                throw new XunitException("Should not throw", ex);
            }

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
            backend.UnpatchAll();
        }
    }

    public void FinalizerOnly_CatchesException_AndDoesNotSuppressIt()
    {
        // Arrange: patch ONLY the finalizer so original throws
        var id = GenerateId(nameof(FinalizerOnly_CatchesException_AndDoesNotSuppressIt));
        var backend = CreateBackend(id);
        TestLoggerProvider.Logger.Clear();

        // Patch Divide with finalizer only
        var originalDivide = typeof(Calculator).GetMethod(nameof(Calculator.Divide),
            BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            types: [typeof(int), typeof(int)],
            modifiers: null);

        Assert.NotNull(originalDivide);

        var finalizer = new PreludeMethod(typeof(CalculatorPatches).GetMethod(nameof(CalculatorPatches.DivideFinalizer), BindingFlags.Public | BindingFlags.Static)!);
        backend.Patch(originalDivide, finalizer: finalizer);

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
            backend.UnpatchAll();
        }
    }

    public void Patched_Divide_NormalOperands_UnchangedResult_AndFinalizerLogsSuccess()
    {
        // Arrange
        var id = GenerateId(nameof(Patched_Divide_NormalOperands_UnchangedResult_AndFinalizerLogsSuccess));
        var backend = CreateBackend(id);
        TestLoggerProvider.Logger.Clear();

        backend.CreateClassProcessor(typeof(CalculatorPatches)).Patch();

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
            backend.UnpatchAll();
        }
    }
}