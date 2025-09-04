using HarmonyWeaver.Core;
using HarmonyWeaver.Core.Implementation;
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

            // Assert
            Assert.Single(patchedFiles);
            Assert.True(File.Exists(outputPath));

            // Verify the patched assembly can be loaded
            var patchedAssembly = Assembly.LoadFrom(outputPath);
            Assert.NotNull(patchedAssembly);

            // Try to create an instance and call the patched method
            var calculatorType = patchedAssembly.GetType("HarmonyWeaver.Examples.Calculator");
            Assert.NotNull(calculatorType);

            var calculator = Activator.CreateInstance(calculatorType);
            Assert.NotNull(calculator);

            var addMethod = calculatorType.GetMethod("Add");
            Assert.NotNull(addMethod);

            // Call the patched method
            var result = addMethod.Invoke(calculator, new object[] { 5, 3 });
            Assert.Equal(8, result);
        }

        [Fact]
        public void PatchCalculatorMultiply_WithSkipPrefix_ShouldReturnCustomResult()
        {
            // Arrange
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

            // Assert
            Assert.Single(patchedFiles);
            Assert.True(File.Exists(outputPath));

            // Load the patched assembly and test the skip logic
            var patchedAssembly = Assembly.LoadFrom(outputPath);
            var calculatorType = patchedAssembly.GetType("HarmonyWeaver.Examples.Calculator");
            var calculator = Activator.CreateInstance(calculatorType);
            var multiplyMethod = calculatorType.GetMethod("Multiply");

            // Test: Multiply should return 999 (from prefix) instead of 5 * 3 = 15
            var result = multiplyMethod.Invoke(calculator, new object[] { 5, 3 });
            Assert.Equal(999, result); // Custom result from prefix, not 15

            // Test conditional skip logic
            var subtractMethod = calculatorType.GetMethod("Subtract");
            
            // Case 1: Should skip and return 42
            var skipResult = subtractMethod.Invoke(calculator, new object[] { 100, 1 });
            Assert.Equal(42, skipResult); // Custom result from prefix
            
            // Case 2: Should NOT skip and return normal result
            var normalResult = subtractMethod.Invoke(calculator, new object[] { 10, 3 });
            Assert.Equal(7, normalResult); // Normal subtraction: 10 - 3 = 7
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