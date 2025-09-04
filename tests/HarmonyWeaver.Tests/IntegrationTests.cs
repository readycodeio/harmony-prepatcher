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
    /// </summary>
    public class IntegrationTests : IDisposable
    {
        private readonly string _testOutputDirectory;
        private readonly Core.HarmonyWeaver _harmonyWeaver;

        public IntegrationTests()
        {
            _testOutputDirectory = Path.Combine(Path.GetTempPath(), "HarmonyWeaverIntegrationTests", Guid.NewGuid().ToString());
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
            // Arrange
            var logFilePath = Path.Combine(_testOutputDirectory, "patch_test.log");
            var fileLogger = new FileLogger(logFilePath);
            LoggerProvider.SetLogger(fileLogger);

            var examplesAssemblyPath = GetAssemblyPath("HarmonyWeaver.Examples.dll");
            var patchesAssemblyPath = GetAssemblyPath("HarmonyWeaver.Tests.Patches.dll");
            
            // Skip test if assemblies don't exist (they need to be built first)
            if (!File.Exists(examplesAssemblyPath) || !File.Exists(patchesAssemblyPath))
            {
                return; // Skip this test for now
            }

            var outputPath = Path.Combine(_testOutputDirectory, "HarmonyWeaver.Examples_patched.dll");

            // Act
            var patchedFiles = _harmonyWeaver.ProcessPatches(
                new[] { patchesAssemblyPath },
                new[] { examplesAssemblyPath },
                new[] { outputPath }
            );

            // Assert patching worked
            Assert.Single(patchedFiles);
            Assert.True(File.Exists(outputPath));

            // Load the patched assembly in an isolated context to avoid conflicts
            using var patchedExecutor = new PatchedAssemblyExecutor(outputPath);
            
            var calculatorType = patchedExecutor.GetType("HarmonyWeaver.Examples.Calculator");
            Assert.NotNull(calculatorType);

            var calculator = patchedExecutor.CreateInstance(calculatorType);
            Assert.NotNull(calculator);

            // Clear any previous log entries
            fileLogger.Clear();

            // Call the patched method using the isolated executor
            var result = patchedExecutor.InvokeMethod(calculator, "Add", 5, 3);
            
            // Verify the method returns the correct result
            Assert.Equal(8, result);

            // Wait a moment for file I/O to complete
            System.Threading.Thread.Sleep(100);

            // Verify the patches were executed by checking the log messages
            Assert.True(fileLogger.ContainsMessage("[PREFIX] About to add 5 + 3"), 
                "Prefix patch should have logged the operation");
            Assert.True(fileLogger.ContainsMessage("[POSTFIX] Addition result: 5 + 3 = 8"), 
                "Postfix patch should have logged the result");
            
            // Verify we have at least 2 log entries (prefix + postfix)
            var logEntries = fileLogger.ReadAllEntries();
            Assert.True(logEntries.Length >= 2, 
                $"Expected at least 2 log entries, but got {logEntries.Length}. Entries: {string.Join("; ", logEntries)}");

            // Clean up
            LoggerProvider.ClearLogger();
        }

        [Fact]
        public void PatchCalculatorMultiply_WithSkipPrefix_ShouldReturnCustomResult()
        {
            // Arrange
            var testLogger = new TestLogger();
            LoggerProvider.SetLogger(testLogger);

            var examplesAssemblyPath = GetAssemblyPath("HarmonyWeaver.Examples.dll");
            var patchesAssemblyPath = GetAssemblyPath("HarmonyWeaver.Tests.Patches.dll");
            
            // Skip test if assemblies don't exist
            if (!File.Exists(examplesAssemblyPath) || !File.Exists(patchesAssemblyPath))
            {
                return;
            }

            var outputPath = Path.Combine(_testOutputDirectory, "HarmonyWeaver.Examples_skip_test.dll");

            // Act
            var patchedFiles = _harmonyWeaver.ProcessPatches(
                new[] { patchesAssemblyPath },
                new[] { examplesAssemblyPath },
                new[] { outputPath }
            );

            // Assert patching worked
            Assert.Single(patchedFiles);
            Assert.True(File.Exists(outputPath));

            // Load the patched assembly in an isolated context
            using var patchedExecutor = new PatchedAssemblyExecutor(outputPath);
            
            var calculatorType = patchedExecutor.GetType("HarmonyWeaver.Examples.Calculator");
            Assert.NotNull(calculatorType);

            var calculator = patchedExecutor.CreateInstance(calculatorType);
            Assert.NotNull(calculator);

            testLogger.Clear();

            // Test: Multiply should return 999 (from prefix) instead of 5 * 3 = 15
            var result = patchedExecutor.InvokeMethod(calculator, "Multiply", 5, 3);
            
            // Verify the skip prefix was called
            Assert.True(testLogger.ContainsMessage("[SKIP PREFIX] Multiply(5, 3) - returning custom result"),
                "Skip prefix should have logged the custom result message");

            // This is the main test - if skip logic works, we should get 999 instead of 15
            Assert.Equal(999, result); // Custom result from prefix, not 15

            testLogger.Clear();

            // Test conditional skip logic
            // Case 1: Should skip and return 42
            var skipResult = patchedExecutor.InvokeMethod(calculator, "Subtract", 100, 1);
            Assert.True(testLogger.ContainsMessage("[CONDITIONAL PREFIX] Special case detected, returning 42"),
                "Conditional prefix should have detected the special case");
            Assert.Equal(42, skipResult); // Custom result from prefix
            
            testLogger.Clear();
            
            // Case 2: Should NOT skip and return normal result
            var normalResult = patchedExecutor.InvokeMethod(calculator, "Subtract", 10, 3);
            Assert.True(testLogger.ContainsMessage("[CONDITIONAL PREFIX] Normal case, continuing with original method"),
                "Conditional prefix should have detected the normal case");
            Assert.Equal(7, normalResult); // Normal subtraction: 10 - 3 = 7

            // Clean up
            LoggerProvider.ClearLogger();
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