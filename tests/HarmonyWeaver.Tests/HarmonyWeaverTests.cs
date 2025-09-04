using HarmonyWeaver.Core;
using HarmonyWeaver.Core.Implementation;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace HarmonyWeaver.Tests
{
    /// <summary>
    /// Basic tests for the HarmonyWeaver functionality
    /// </summary>
    public class HarmonyWeaverTests : IDisposable
    {
        private readonly string _testOutputDirectory;
        private readonly Core.HarmonyWeaver _harmonyWeaver;

        public HarmonyWeaverTests()
        {
            var uniqueId = $"{DateTime.Now:yyyyMMdd_HHmmss_fff}_{Environment.CurrentManagedThreadId}_{Guid.NewGuid():N}";
            _testOutputDirectory = Path.Combine(Path.GetTempPath(), "HarmonyWeaverTests", uniqueId);
            Directory.CreateDirectory(_testOutputDirectory);

            // Create the weaver with default implementations
            var cecilAssemblyLoader = new FlexibleCecilAssemblyLoader();
            var patchScanner = new PatchScanner();
            var ilWeaver = new ILWeaver();
            var assemblySaver = new AssemblySaver();

            _harmonyWeaver = new Core.HarmonyWeaver(cecilAssemblyLoader, patchScanner, ilWeaver, assemblySaver);
        }

        [Fact]
        public void Constructor_WithNullParameters_ThrowsArgumentNullException()
        {
            // Test that constructor validates parameters
            Assert.Throws<ArgumentNullException>(() => new Core.HarmonyWeaver(null, null, null, null));
        }

        [Fact]
        public void ProcessPatches_WithNullParameters_ThrowsArgumentNullException()
        {
            // Test parameter validation
            Assert.Throws<ArgumentNullException>(() => 
                _harmonyWeaver.ProcessPatches(null, new[] { "test" }, "output"));
            
            Assert.Throws<ArgumentNullException>(() => 
                _harmonyWeaver.ProcessPatches(new[] { "test" }, null, "output"));
            
            Assert.Throws<ArgumentNullException>(() => 
                _harmonyWeaver.ProcessPatches(new[] { "test" }, new[] { "test" }, (string)null));
        }

        [Fact]
        public void ProcessPatches_WithMismatchedArrays_ThrowsArgumentException()
        {
            // Test that target and output arrays must have same length
            Assert.Throws<ArgumentException>(() => 
                _harmonyWeaver.ProcessPatches(
                    new[] { "patch1" }, 
                    new[] { "target1", "target2" }, 
                    new[] { "output1" }));
        }

        [Fact]
        public void ProcessPatches_WithNonExistentFiles_ThrowsException()
        {
            // Test handling of non-existent files
            var patchPaths = new[] { "nonexistent_patch.dll" };
            var targetPaths = new[] { "nonexistent_target.dll" };
            var outputPaths = new[] { Path.Combine(_testOutputDirectory, "output.dll") };

            var exception = Assert.Throws<InvalidOperationException>(() =>
                _harmonyWeaver.ProcessPatches(patchPaths, targetPaths, outputPaths));

            Assert.Contains("Error processing patches", exception.Message);
        }

        // TODO: Add integration tests when we have working assemblies to test with
        // These would require building the example and patch assemblies first

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