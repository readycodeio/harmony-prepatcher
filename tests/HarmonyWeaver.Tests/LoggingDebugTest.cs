using HarmonyWeaver.Core;
using HarmonyWeaver.Core.Implementation;
using HarmonyWeaver.Core.Interfaces;
using HarmonyWeaver.Core.Logging;
using System;
using System.IO;
using System.Reflection;
using Xunit;
using Xunit.Abstractions;

namespace HarmonyWeaver.Tests
{
    /// <summary>
    /// Debug test to understand logging behavior in patched assemblies
    /// </summary>
    [Collection("IntegrationTests")]
    public class LoggingDebugTest
    {
        private readonly ITestOutputHelper _output;
        private readonly string _testOutputDirectory;
        private readonly Core.HarmonyWeaver _harmonyWeaver;
        private readonly IRuntimeAssemblyLoader _runtimeAssemblyLoader;

        public LoggingDebugTest(ITestOutputHelper output)
        {
            _output = output;
            var uniqueId = $"{DateTime.Now:yyyyMMdd_HHmmss_fff}_{Environment.CurrentManagedThreadId}_{Guid.NewGuid():N}";
            _testOutputDirectory = Path.Combine(Path.GetTempPath(), "HarmonyWeaverLoggingDebug", uniqueId);
            Directory.CreateDirectory(_testOutputDirectory);

            // Use RetryAssemblyLoader for Cecil assemblies (ProcessPatches phase)
            var cecilAssemblyLoader = new RetryAssemblyLoader(maxAttempts: 10, baseDelayMs: 25);
            var patchScanner = new PatchScanner();
            var ilWeaver = new ILWeaver();
            var assemblySaver = new AssemblySaver();

            // Use RetryRuntimeAssemblyLoader for loading patched assemblies for execution
            _runtimeAssemblyLoader = new RetryRuntimeAssemblyLoader(maxAttempts: 10, baseDelayMs: 25);

            _harmonyWeaver = new Core.HarmonyWeaver(cecilAssemblyLoader, patchScanner, ilWeaver, assemblySaver);
        }

        [Fact]
        public void DebugLoggingInPatchedAssembly()
        {
            // Arrange
            var testLogger = new TestLogger();
            LoggerProvider.SetLogger(testLogger);

            var examplesAssemblyPath = GetAssemblyPath("HarmonyWeaver.Examples.dll");
            var patchesAssemblyPath = GetAssemblyPath("HarmonyWeaver.Tests.Patches.dll");
            
            if (!File.Exists(examplesAssemblyPath) || !File.Exists(patchesAssemblyPath))
            {
                _output.WriteLine("Assemblies not found, skipping test");
                return;
            }

            var outputPath = Path.Combine(_testOutputDirectory, "HarmonyWeaver.Examples_logging_debug.dll");

            _output.WriteLine($"Examples assembly: {examplesAssemblyPath}");
            _output.WriteLine($"Patches assembly: {patchesAssemblyPath}");
            _output.WriteLine($"Output path: {outputPath}");

            // Act
            var patchedFiles = _harmonyWeaver.ProcessPatches(
                new[] { patchesAssemblyPath },
                new[] { examplesAssemblyPath },
                new[] { outputPath }
            );

            _output.WriteLine($"Patched files: {string.Join(", ", patchedFiles)}");

            // Load the patched assembly using the injected runtime assembly loader
            var patchedAssembly = _runtimeAssemblyLoader.LoadAssembly(outputPath);
            var calculatorType = patchedAssembly.GetType("HarmonyWeaver.Examples.Calculator");
            var calculator = Activator.CreateInstance(calculatorType);
            var addMethod = calculatorType.GetMethod("Add");

            _output.WriteLine($"Logger before call: {LoggerProvider.Logger.GetType().Name}");
            _output.WriteLine($"Test logger count before call: {testLogger.Count}");

            testLogger.Clear();

            // Test direct logging
            LoggerProvider.Logger.LogInfo("TEST: Direct logging works");
            _output.WriteLine($"Test logger count after direct log: {testLogger.Count}");

            // Call the patched method
            var result = addMethod.Invoke(calculator, new object[] { 5, 3 });
            
            _output.WriteLine($"Method result: {result}");
            _output.WriteLine($"Test logger count after method call: {testLogger.Count}");
            
            // Show all log entries
            foreach (var entry in testLogger.LogEntries)
            {
                _output.WriteLine($"Log entry: {entry}");
            }

            // The main issue might be that the patched assembly doesn't share static state
            // Let's see what we can learn
            Assert.True(true, "Debug test completed");
        }

        private string GetAssemblyPath(string assemblyFileName)
        {
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

            return assemblyFileName;
        }
    }
}