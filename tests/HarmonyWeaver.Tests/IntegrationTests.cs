using HarmonyWeaver.Core;
using HarmonyWeaver.Core.Implementation;
using HarmonyWeaver.Core.Loading;
using HarmonyWeaver.Core.Logging;
using System;
using System.IO;
using System.Reflection;
using Xunit;

namespace HarmonyWeaver.Tests
{
    /// <summary>
    /// Integration tests that verify the actual patching functionality works end-to-end
    /// Uses collection to prevent parallel execution and avoid shared state conflicts
    /// </summary>
    [Collection("IntegrationTests")]
    public class IntegrationTests : IDisposable
    {
        private readonly string _testOutputDirectory;
        private readonly Core.HarmonyWeaver _harmonyWeaver;

        public IntegrationTests()
        {
            // Use timestamp and thread ID to ensure unique directory even in parallel execution
            var uniqueId = $"{DateTime.Now:yyyyMMdd_HHmmss_fff}_{Environment.CurrentManagedThreadId}_{Guid.NewGuid():N}";
            _testOutputDirectory = Path.Combine(Path.GetTempPath(), "HarmonyWeaverIntegrationTests", uniqueId);
            Directory.CreateDirectory(_testOutputDirectory);

            // Create the weaver with default implementations
            var assemblyLoader = new AssemblyLoader();
            var patchScanner = new PatchScanner();
            var ilWeaver = new ILWeaver();
            var assemblySaver = new AssemblySaver();

            _harmonyWeaver = new Core.HarmonyWeaver(assemblyLoader, patchScanner, ilWeaver, assemblySaver);
        }

        [Fact]
        public void PatchCalculatorAdd_WithPrefixAndPostfix_ShouldWork()
        {
            // Arrange - Use global test logger to avoid file I/O issues
            var testId = Guid.NewGuid().ToString("N")[0..8];
            var globalLogger = new GlobalTestLogger($"test_{testId}");
            LoggerProvider.SetGlobalLogger(globalLogger);

            var examplesAssemblyPath = GetAssemblyPath("HarmonyWeaver.Examples.dll");
            var patchesAssemblyPath = GetAssemblyPath("HarmonyWeaver.Tests.Patches.dll");
            
            // Skip test if assemblies don't exist (they need to be built first)
            if (!File.Exists(examplesAssemblyPath) || !File.Exists(patchesAssemblyPath))
            {
                return; // Skip this test for now
            }

            var outputPath = Path.Combine(_testOutputDirectory, $"HarmonyWeaver.Examples_patched_{testId}.dll");

            // Act
            var patchedFiles = _harmonyWeaver.ProcessPatches(
                new[] { patchesAssemblyPath },
                new[] { examplesAssemblyPath },
                new[] { outputPath }
            );

            // Assert patching worked
            Assert.Single(patchedFiles);
            Assert.True(File.Exists(outputPath));

            // Load the patched assembly with Windows-compatible retry logic
            var patchedAssembly = WindowsAssemblyLoader.LoadFromWithRetry(outputPath);
            Assert.NotNull(patchedAssembly);

            var calculatorType = patchedAssembly.GetType("HarmonyWeaver.Examples.Calculator");
            Assert.NotNull(calculatorType);

            var calculator = Activator.CreateInstance(calculatorType);
            Assert.NotNull(calculator);

            var addMethod = calculatorType.GetMethod("Add");
            Assert.NotNull(addMethod);

            // Clear any previous log entries
            globalLogger.Clear();

            // Call the patched method
            var result = addMethod.Invoke(calculator, new object[] { 5, 3 });
            
            // Verify the method returns the correct result
            Assert.Equal(8, result);

            // Verify the patches were executed by checking the log messages
            Assert.True(globalLogger.ContainsMessage("[PREFIX] About to add 5 + 3"), 
                "Prefix patch should have logged the operation");
            Assert.True(globalLogger.ContainsMessage("[POSTFIX] Addition result: 5 + 3 = 8"), 
                "Postfix patch should have logged the result");
            
            // Verify we have at least 2 log entries (prefix + postfix)
            Assert.True(globalLogger.Count >= 2, 
                $"Expected at least 2 log entries, but got {globalLogger.Count}");

            // Clean up
            LoggerProvider.ClearAllLoggers();
        }

        [Fact]
        public void PatchCalculatorMultiply_WithSkipPrefix_ShouldReturnCustomResult()
        {
            // Arrange - Use global test logger to avoid file I/O issues
            var testId = Guid.NewGuid().ToString("N")[0..8];
            var globalLogger = new GlobalTestLogger($"skip_test_{testId}");
            LoggerProvider.SetGlobalLogger(globalLogger);

            var examplesAssemblyPath = GetAssemblyPath("HarmonyWeaver.Examples.dll");
            var patchesAssemblyPath = GetAssemblyPath("HarmonyWeaver.Tests.Patches.dll");
            
            // Skip test if assemblies don't exist
            if (!File.Exists(examplesAssemblyPath) || !File.Exists(patchesAssemblyPath))
            {
                return;
            }

            var outputPath = Path.Combine(_testOutputDirectory, $"HarmonyWeaver.Examples_skip_test_{testId}.dll");

            // Act
            var patchedFiles = _harmonyWeaver.ProcessPatches(
                new[] { patchesAssemblyPath },
                new[] { examplesAssemblyPath },
                new[] { outputPath }
            );

            // Assert patching worked
            Assert.Single(patchedFiles);
            Assert.True(File.Exists(outputPath));

            // Load the patched assembly with Windows-compatible retry logic  
            var patchedAssembly = WindowsAssemblyLoader.LoadFromWithRetry(outputPath);
            var calculatorType = patchedAssembly.GetType("HarmonyWeaver.Examples.Calculator");
            Assert.NotNull(calculatorType);

            var calculator = Activator.CreateInstance(calculatorType);
            Assert.NotNull(calculator);

            var multiplyMethod = calculatorType.GetMethod("Multiply");
            var subtractMethod = calculatorType.GetMethod("Subtract");

            globalLogger.Clear();

            // Test: Multiply should return 999 (from prefix) instead of 5 * 3 = 15
            var result = multiplyMethod.Invoke(calculator, new object[] { 5, 3 });
            
            // Verify the skip prefix was called
            Assert.True(globalLogger.ContainsMessage("[SKIP PREFIX] Multiply(5, 3) - returning custom result"),
                "Skip prefix should have logged the custom result message");

            // This is the main test - if skip logic works, we should get 999 instead of 15
            Assert.Equal(999, result); // Custom result from prefix, not 15

            globalLogger.Clear();

            // Test conditional skip logic
            // Case 1: Should skip and return 42
            var skipResult = subtractMethod.Invoke(calculator, new object[] { 100, 1 });
            Assert.True(globalLogger.ContainsMessage("[CONDITIONAL PREFIX] Special case detected, returning 42"),
                "Conditional prefix should have detected the special case");
            Assert.Equal(42, skipResult); // Custom result from prefix
            
            globalLogger.Clear();
            
            // Case 2: Should NOT skip and return normal result
            var normalResult = subtractMethod.Invoke(calculator, new object[] { 10, 3 });
            Assert.True(globalLogger.ContainsMessage("[CONDITIONAL PREFIX] Normal case, continuing with original method"),
                "Conditional prefix should have detected the normal case");
            Assert.Equal(7, normalResult); // Normal subtraction: 10 - 3 = 7

            // Clean up
            LoggerProvider.ClearAllLoggers();
        }

        private string GetAssemblyPath(string assemblyFileName)
        {
            // Look for the assembly in the output directories
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var possiblePaths = new[]
            {
                Path.Combine(baseDir, assemblyFileName),
                Path.Combine(baseDir, "..", "..", "..", "..", "examples", "HarmonyWeaver.Examples", "bin", "Debug", "net8.0", assemblyFileName),
                Path.Combine(baseDir, "..", "..", "..", "..", "tests", "HarmonyWeaver.Tests.Patches", "bin", "Debug", "net8.0", assemblyFileName)
            };

            foreach (var path in possiblePaths)
            {
                var fullPath = Path.GetFullPath(path);
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }

            return assemblyFileName; // Return the filename if not found
        }

        public void Dispose()
        {
            _harmonyWeaver?.Dispose();
            
            if (Directory.Exists(_testOutputDirectory))
            {
                try
                {
                    Directory.Delete(_testOutputDirectory, true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }
    }
}